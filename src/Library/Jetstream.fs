namespace Library

open System
open System.Collections.Generic
open System.IO.Pipelines
open System.Net
open System.Net.Http
open System.Net.WebSockets
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization
open System.Threading

open Flurl
open Flurl.Http

open FSharp.Control
open IcedTasks
open IcedTasks.Polyfill.Async

open FsToolkit.ErrorHandling

type BskyStrongRef = { cid: string; uri: string }

type BskyReplyRef = {
  root: BskyStrongRef
  parent: BskyStrongRef
}

// partial post, we don't need all the fields
type BskFeedPost = {
  text: string
  createdAt: DateTimeOffset
  reply: BskyReplyRef option
  tags: string list option
}

type Commit = {
  rev: string
  operation: string
  collection: string
  rkey: string
  record: BskFeedPost option
  cid: string
}

type Identity = {
  did: string
  handle: string
  seq: int64
  time: string
}

type Account = {
  active: bool
  did: string
  seq: int64
  time: string
}

type Event = {
  did: string
  time_us: int64
  commit: Commit option
  account: Account option
  identity: Identity option
}

type Profile = {
  did: string
  handle: string
  displayName: string
  description: string voption
  avatar: Uri voption
  banner: Uri voption
  followersCount: int
  followsCount: int
  postsCount: int
  createdAt: DateTimeOffset
}



module Profile =

  let ofJsonNode (json: JsonNode) = option {
    let profile = json.AsObject()

    let! did =
      profile |> JsonNode.tryGetProperty "did" |> ValueOption.map(_.GetValue())

    let! handle =
      profile
      |> JsonNode.tryGetProperty "handle"
      |> ValueOption.map(_.GetValue())

    let! displayName =
      profile
      |> JsonNode.tryGetProperty "displayName"
      |> ValueOption.map(_.GetValue())

    let description =
      profile
      |> JsonNode.tryGetProperty "description"
      |> ValueOption.map(_.GetValue())

    let avatar =
      profile
      |> JsonNode.tryGetProperty "avatar"
      |> ValueOption.map(fun v -> v.GetValue() |> Uri)

    let banner =
      profile
      |> JsonNode.tryGetProperty "banner"
      |> ValueOption.map(fun v -> v.GetValue() |> Uri)

    let! followersCount =
      profile
      |> JsonNode.tryGetProperty "followersCount"
      |> ValueOption.map(_.GetValue())

    let! followsCount =
      profile
      |> JsonNode.tryGetProperty "followsCount"
      |> ValueOption.map(_.GetValue())

    let! postsCount =
      profile
      |> JsonNode.tryGetProperty "postsCount"
      |> ValueOption.map(_.GetValue())

    let! createdAt =
      profile
      |> JsonNode.tryGetProperty "createdAt"
      |> ValueOption.bind(fun v ->
        match DateTimeOffset.TryParse(v.GetValue<string>()) with
        | true, value -> ValueSome value
        | _ -> ValueNone
      )

    return {
      did = did
      handle = handle
      displayName = displayName
      description = description
      avatar = avatar
      banner = banner
      followersCount = followersCount
      followsCount = followsCount
      postsCount = postsCount
      createdAt = createdAt
    }

  }

type BskyJetstream =

  abstract resolveHandle: did: string -> Async<Profile option>

  abstract toAsyncSeq:
    uri: string * ?cancellationToken: CancellationToken ->
      IAsyncEnumerable<Result<Event, exn * string>>

  abstract toObservable:
    uri: string * ?cancellationToken: CancellationToken ->
      IObservable<Result<Event, exn * string>>


module JetStream =

  let private startListening (uri: Uri, token) = taskSeq {
    let jsonOptions =
      JsonSerializerOptions(
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
      )

    use handler = new SocketsHttpHandler()
    use ws = new ClientWebSocket()
    ws.Options.HttpVersion <- HttpVersion.Version20
    ws.Options.HttpVersionPolicy <- HttpVersionPolicy.RequestVersionOrHigher

    do! ws.ConnectAsync(uri, new HttpMessageInvoker(handler), token)

    let pipe = Pipe()

    while ws.State = WebSocketState.Open do
      if token.IsCancellationRequested then
        do!
          ws.CloseAsync(
            WebSocketCloseStatus.NormalClosure,
            "Cancelled",
            CancellationToken.None
          )
      else

        let buffer = pipe.Writer.GetMemory(1024 * 4)
        let! result = ws.ReceiveAsync(buffer, token)
        pipe.Writer.Advance(result.Count)

        if result.EndOfMessage then
          let! _ = pipe.Writer.FlushAsync(token)
          do! pipe.Writer.CompleteAsync()
          let! read = pipe.Reader.ReadAsync(token)

          try
            let result =
              JsonSerializer.Deserialize<Event>(
                read.Buffer.FirstSpan,
                jsonOptions
              )

            match result with
            | null -> ()
            | result -> Ok result
          with ex ->
            let json = Text.Encoding.UTF8.GetString(read.Buffer.FirstSpan)
            Error(ex, json)

          pipe.Reader.AdvanceTo(read.Buffer.End)
          do! pipe.Reader.CompleteAsync()
          pipe.Reset()

    match ws.State with
    | WebSocketState.Aborted ->
      // Notify that we finished abnormally
      failwith "The connection was closed"
    | _ -> ()

    do! pipe.Writer.CompleteAsync()
    do! pipe.Reader.CompleteAsync()
    pipe.Reset()
  }

  let private toObservable (uri, token) =
    { new IObservable<_> with
        member _.Subscribe(observer: IObserver<_>) =
          let token = defaultArg token CancellationToken.None
          let cts = CancellationTokenSource.CreateLinkedTokenSource(token)

          let work = async {
            try
              do!
                startListening(Uri(uri), cts.Token)
                |> TaskSeq.iter observer.OnNext

              observer.OnCompleted()
            with
            | :? OperationCanceledException -> ()
            | ex -> observer.OnError(ex)
          }

          Async.StartImmediate(work, cts.Token)

          { new IDisposable with
              member _.Dispose() =
                cts.Cancel()
                cts.Dispose()
          }
    }

  let create () =
    { new BskyJetstream with
        member _.resolveHandle did = async {
          let! token = Async.CancellationToken

          try
            let! profile =
              $"https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor={did}"
                .GetJsonAsync<JsonNode>(cancellationToken = token)

            return Profile.ofJsonNode profile
          with _ ->
            return None
        }

        member _.toAsyncSeq(uri, ?token) =
          startListening(Uri(uri), defaultArg token CancellationToken.None)

        member _.toObservable(uri, ?token) = toObservable(uri, token)
    }

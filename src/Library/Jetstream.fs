namespace Library

open System
open System.Collections.Generic
open System.IO.Pipelines
open System.Net.WebSockets
open System.Net
open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization
open System.Threading

open Flurl
open Flurl.Http

open FSharp.Control
open IcedTasks
open IcedTasks.Polyfill.Async

open FsToolkit.ErrorHandling

type Commit = {
  rev: string
  operation: string
  collection: string
  rkey: string
  record: JsonDocument option
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
  description: string option
  avatar: Uri
  banner: Uri option
  followersCount: int
  followsCount: int
  postsCount: int
  createdAt: DateTimeOffset
}

module Profile =
  let ofJsonDocument (json: JsonDocument) = option {
    let profile = json.RootElement
    let did = profile.GetProperty("did").GetString()

    let handle =
      profile
        .GetProperty("handle")
        .GetString()

    let displayName =
      profile
        .GetProperty("displayName")
        .GetString()

    let description =
      try
        profile
          .GetProperty("description")
          .GetString()
        |> Some
      with _ ->
        None

    let avatar =
      profile
        .GetProperty("avatar")
        .GetString()
      |> Uri

    let banner =
      try
        profile
          .GetProperty("banner")
          .GetString()
        |> Uri
        |> Some
      with _ ->
        None

    let! followersCount =
      let prop = profile.GetProperty("followersCount")

      match prop.TryGetInt32() with
      | true, count -> ValueSome count
      | _ -> ValueNone

    let! followsCount =
      let prop = profile.GetProperty("followsCount")

      match prop.TryGetInt32() with
      | true, count -> ValueSome count
      | _ -> ValueNone

    let! postsCount =
      let prop = profile.GetProperty("postsCount")

      match prop.TryGetInt32() with
      | true, count -> ValueSome count
      | _ -> ValueNone

    let! createdAt =
      let prop = profile.GetProperty("createdAt")

      match prop.TryGetDateTimeOffset() with
      | true, time -> ValueSome time
      | _ -> ValueNone

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

  abstract resolveHandle: did: string -> Async<BSky.Model.AppBskyActorDefsProfileViewDetailed option>

  abstract toAsyncSeq:
    uri: string * ?cancellationToken: CancellationToken ->
      IAsyncEnumerable<Result<Event, exn * string>>

  abstract toObservable:
    uri: string * ?cancellationToken: CancellationToken ->
      IObservable<Result<Event, exn * string>>


module JetStream =

  let private startListening (uri: Uri, token) =
    let pipe = Pipe()

    let jsonOptions =
      JsonSerializerOptions(
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
      )

    taskSeq {

      use handler = new SocketsHttpHandler()
      use ws = new ClientWebSocket()
      ws.Options.HttpVersion <- HttpVersion.Version20
      ws.Options.HttpVersionPolicy <- HttpVersionPolicy.RequestVersionOrHigher

      do! ws.ConnectAsync(uri, new HttpMessageInvoker(handler), token)

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
            let! read = pipe.Reader.ReadAsync(token)

            try
              let result =
                JsonSerializer.Deserialize<Event>(
                  read.Buffer.FirstSpan,
                  jsonOptions
                )

              Ok result
            with ex ->
              let json = Text.Encoding.UTF8.GetString(read.Buffer.FirstSpan)
              Error(ex, json)

            pipe.Reader.AdvanceTo(read.Buffer.End)

      match ws.State with
      | WebSocketState.Aborted ->
        // Notify that we finished abnormally
        failwith "The connection was closed"
      | _ -> ()
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

  let create (bsky: BSky.Api.AppBskyActorApi) =
    { new BskyJetstream with
        member _.resolveHandle did = async {
          let! token = Async.CancellationToken

          try
            let! profile =
              bsky.AppBskyActorGetProfileAsync(did, cancellationToken = token)

            return Some profile
          with _ ->
            return None
        }

        member _.toAsyncSeq(uri, ?token) =
          startListening(Uri(uri), defaultArg token CancellationToken.None)

        member _.toObservable(uri, ?token) = toObservable(uri, token)
    }

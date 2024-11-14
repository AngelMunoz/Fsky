module Library.Stores.Home

open System
open System.ComponentModel
open System.Collections.ObjectModel
open System.Threading
open FSharp.Control
open FSharp.Control.Reactive
open FsToolkit.ErrorHandling
open Library
open Library.Env
open System.Reactive.Subjects


type Post = {
  did: string
  handle: string
  displayName: string
  avatar: string
  text: string
  time: DateTimeOffset
}

type Credentials = { handle: string; password: string }

[<AutoOpen>]
module private HomeStore =

  let (|NotCancelled|_|) (token: CancellationToken option) =
    token.IsSome && not token.Value.IsCancellationRequested

  let mapEventToPost value =
    result {
      let! event = value |> Result.mapError snd

      let! commit = event.commit |> Result.requireSome ""
      let! record = commit.record |> Result.requireSome ""
      let record = record

      let did = event.did

      let! text = record.text |> Result.requireNotNull ""

      let time = record.createdAt

      return {
        did = did
        handle = ""
        displayName = ""
        avatar = ""
        text = text
        time = time
      }
    }
    |> Result.toOption

  let resolveHandle (js: BskyJetstream) =
    fun event -> taskOption {
      let! post = mapEventToPost event
      let! profile = js.resolveHandle post.did

      return {
        post with
            handle = profile.handle
            displayName = profile.displayName
            avatar =
              profile.avatar
              |> ValueOption.map(_.ToString())
              |> ValueOption.defaultWith(fun _ ->
                "https://via.placeholder.com/32"
              )
      }
    }


type HomeStore(js: BskyJetstream) =

  let _posts = ObservableCollection<Post>()
  let mutable cts = new CancellationTokenSource()

  [<Literal>]
  let url =
    "wss://jetstream2.us-east.bsky.network/subscribe?wantedCollections=app.bsky.feed.post"

  let resetCts (token: CancellationToken option) =

    try
      if not cts.IsCancellationRequested then
        cts.Cancel()

      cts.Dispose()
    with _ ->
      ()

    match token with
    | Some token & NotCancelled ->
      cts <- CancellationTokenSource.CreateLinkedTokenSource(token)
    | Some _
    | None -> cts <- new CancellationTokenSource()


  let getEventStream token =
    resetCts(token)

    js.toObservable(
      "wss://jetstream2.us-east.bsky.network/subscribe?wantedCollections=app.bsky.feed.post",
      cts.Token
    )

  let dispositions = ResizeArray<IDisposable>()

  let addToPosts (posts: ObservableCollection<_>) post =
    if posts.Count >= 10 then
      posts.RemoveAt(posts.Count - 1)

    posts.Insert(0, post)

  member _.credentials = Subject.behavior({ handle = ""; password = "" })
  member _.posts = _posts

  member _.loadPosts(?token) =
    let events = js.toAsyncSeq(url, ?cancellationToken = token)

    let initialLoad =
      events
      |> TaskSeq.take 20
      |> TaskSeq.chooseAsync(resolveHandle js)
      |> TaskSeq.iterAsync(fun post -> task {
        addToPosts _posts post
        do! Async.Sleep(2500)
      })
      |> Async.AwaitTask

    Async.StartImmediate(initialLoad, ?cancellationToken = token)

  member _.stopPostStream() =
    resetCts None
    _posts.Clear()

  interface IDisposable with
    member _.Dispose() =
      cts.Dispose()

      for disposition in dispositions do
        disposition.Dispose()


  static member create(Jetstream js) = new HomeStore(js)

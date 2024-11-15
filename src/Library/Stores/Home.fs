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

      let text = record.text

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


  let getEventStream token =
    js.toObservable(
      "wss://jetstream2.us-east.bsky.network/subscribe?wantedCollections=app.bsky.feed.post",
      token
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
      |> TaskSeq.take 10
      |> TaskSeq.chooseAsync(resolveHandle js)
      |> TaskSeq.iterAsync(fun post -> task {
        addToPosts _posts post
        let sleep = Random.Shared.Next(1000, 5000)
        do! Async.Sleep(sleep)
      })
      |> Async.AwaitTask

    Async.StartImmediate(initialLoad, ?cancellationToken = token)

  interface IDisposable with
    member _.Dispose() =
      cts.Dispose()

      for disposition in dispositions do
        disposition.Dispose()


  static member create(Jetstream js) = new HomeStore(js)

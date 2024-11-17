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
open Library.Services
open Library.Services.JetstreamTypes

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

  let toPost (js: BskyAPI) =
    fun event -> asyncOption {
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


type HomeStore(js: BskyJetstream, bs: BskyAPI) =

  let _posts = ObservableCollection<Post>()

  [<Literal>]
  let url =
    "wss://jetstream2.us-east.bsky.network/subscribe?wantedCollections=app.bsky.feed.post"

  member _.posts = _posts

  member _.credentials = Subject.behavior({ handle = ""; password = "" })

  member _.loadPosts(?token) =
    let work =
      js.toAsyncSeq(url, ?cancellationToken = token)
      |> AsyncSeq.take 25
      |> AsyncSeq.chooseAsync(fun v -> asyncOption {
        let! result = toPost bs v
        return result
      })
      |> AsyncSeq.iterAsync(fun post -> async {
        if _posts.Count < 5 then
          _posts.Insert(0, post)
          do! Async.Sleep(750)
        else
          do! Async.Sleep(2500)
          _posts.Insert(0, post)
      })

    Async.StartImmediate(work, ?cancellationToken = token)

  interface IDisposable with
    member _.Dispose() = ()

  static member create(Jetstream js & Bluesky bs) = new HomeStore(js, bs)

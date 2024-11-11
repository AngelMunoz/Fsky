module Library.Stores

open System
open System.ComponentModel
open System.Collections.ObjectModel
open System.Threading
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
  time: DateTime
}

type Credentials = { handle: string; password: string }

type HomeStore = {
  credentials: BehaviorSubject<Credentials>
  posts: ObservableCollection<Post>
  loadPosts: CancellationToken option -> unit
  stopPostStream: unit -> unit
}

module HomeStore =

  let private (|NotCancelled|_|) (token: CancellationToken option) =
    match token with
    | None -> None
    | Some token -> if token.IsCancellationRequested then None else Some()


  let private mapEventToPost value =
    result {
      let! event = value |> Result.mapError snd

      let! commit = event.commit |> Result.requireSome ""
      let! record = commit.record |> Result.requireSome ""
      let record = record.RootElement

      let did = event.did

      let! text =
        match record.TryGetProperty("text") with
        | true, text -> text.GetString() |> Ok
        | _ -> Error ""

      let! time =
        match record.TryGetProperty("createdAt") with
        | true, time -> time.GetDateTime() |> Ok
        | _ -> Error ""

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
    fun event -> asyncOption {
      let! post = mapEventToPost event
      let! profile = js.resolveHandle post.did

      return {
        post with
            handle = profile.Handle
            displayName = profile.DisplayName
            avatar = profile.Avatar |> Option.ofObj |> Option.defaultWith(fun _ -> "https://via.placeholder.com/32")
      }
    }

  let create (Jetstream js) =
    let posts = ObservableCollection<Post>()
    let mutable cts = new CancellationTokenSource()

    let resetCts (token: CancellationToken option) =
      if not cts.IsCancellationRequested then
        cts.Cancel()

      try
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

    let addToPosts (posts: ObservableCollection<_>) post =
      match post with
      | Some post ->
        if posts.Count >= 10 then
          posts.RemoveAt(posts.Count - 1)

        posts.Insert(0, post)
        printfn "Post Count: %i" posts.Count
      | None -> ()

    {
      credentials = Subject.behavior({ handle = ""; password = "" })
      posts = posts
      loadPosts =
        fun token ->
          printfn "Loading posts..."

          let events = getEventStream(token)

          events
          |> Observable.bufferCount 10
          |> Observable.first
          |> Observable.map(fun values ->
            values |> Seq.map(resolveHandle js) |> Async.Parallel
          )
          |> Observable.switchAsync
          |> Observable.add(fun values ->
            for value in values do
              addToPosts posts value
          )

          events
          |> Observable.sample(TimeSpan.FromSeconds(2.5))
          |> Observable.map(resolveHandle js)
          |> Observable.switchAsync
          |> Observable.add(addToPosts posts)

      stopPostStream = fun () ->
        resetCts None
        posts.Clear()
    }

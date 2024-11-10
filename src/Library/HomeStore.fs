module Library.Stores

open System
open System.ComponentModel
open System.Collections.ObjectModel
open System.Threading
open FSharp.Control.Reactive
open FsToolkit.ErrorHandling
open Library
open Library.Env


type Post = {
  did: string
  handle: string
  text: string
  time: DateTime
}

type HomeStore = {
  posts: ObservableCollection<Post>
  loadPosts: unit -> unit
  stopPostStream: unit -> unit
}

module HomeStore =

  let create (Jetstream js) =
    let posts = ObservableCollection<Post>()
    let mutable cts = new CancellationTokenSource()

    let resetCts () =
      if not cts.IsCancellationRequested then
        cts.Cancel()

      try
        cts.Dispose()
      with _ ->
        ()

      cts <- new CancellationTokenSource()

    let getEventStream () =
      resetCts()

      js.toObservable(
        "wss://jetstream1.us-east.bsky.network/subscribe?wantedCollections=app.bsky.feed.post",
        cts.Token
      )
      |> Observable.sample(TimeSpan.FromSeconds(1))

    {
      posts = posts
      loadPosts =
        fun () ->
          getEventStream()
          |> Observable.add(fun value ->
            result {
              let! event = value |> Result.mapError snd

              let! commit = event.commit |> Result.requireSome ""
              let! record = commit.record |> Result.requireSome ""
              let record = record.RootElement

              let did = event.did

              let handle = ""

              let! text =
                match record.TryGetProperty("text") with
                | true, text -> text.GetString() |> Ok
                | _ -> Error ""

              let! time =
                match record.TryGetProperty("createdAt") with
                | true, time -> time.GetDateTime() |> Ok
                | _ -> Error ""

              if posts.Count > 10 then
                posts.RemoveAt(0)

              posts.Add(
                {
                  did = did
                  handle = handle
                  text = text
                  time = time
                }
              )

              return ()
            }
            |> Result.teeError(fun err -> printfn "%s" err)
            |> Result.ignoreError
          )

      stopPostStream = resetCts
    }

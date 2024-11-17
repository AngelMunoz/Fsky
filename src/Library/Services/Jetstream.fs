namespace Library.Services

open System
open System.Buffers
open System.IO.Pipelines
open System.Net.WebSockets
open System.Text.Json
open System.Threading

open FSharp.Control
open IcedTasks
open IcedTasks.Polyfill.Async

open Library.Services.JetstreamTypes

type BskyJetstream =

  abstract toAsyncSeq:
    uri: string * ?cancellationToken: CancellationToken ->
      AsyncSeq<Result<Event, exn * string>>

module JetStream =
  open type System.Text.Encoding

  let private jetstreamAsAsyncSeq (uri: Uri, token) =

    let writeMemory (ws: ClientWebSocket, writer: PipeWriter) = async {
      let! token = Async.CancellationToken
      let buffer = writer.GetMemory(512)

      let! result = ws.ReceiveAsync(buffer, token)

      writer.Advance(result.Count)

      return result.EndOfMessage
    }

    let readMemory (reader: PipeReader) = async {
      let! token = Async.CancellationToken
      let! read = reader.ReadAsync(token)

      try
        let bufferBytes = read.Buffer.ToArray()
        let bytes = ReadOnlySpan(bufferBytes)

        let result = JsonSerializer.Deserialize<Event>(bytes)
        reader.AdvanceTo(read.Buffer.End)
        do! reader.CompleteAsync()

        match result with
        | null ->
          return
            Error(
              exn "Failed to deserialize event",
              UTF8.GetString(ReadOnlySpan(bufferBytes))
            )
        | event -> return Ok event
      with ex ->
        let json = Text.Encoding.UTF8.GetString(read.Buffer.FirstSpan)
        return (Error(ex, json))
    }

    asyncSeq {
      use ws = new ClientWebSocket()
      let pipe = Pipe()

      do! ws.ConnectAsync(uri, token) |> Async.AwaitTask

      while ws.State = WebSocketState.Open do
        if token.IsCancellationRequested then
          do!
            ws.CloseAsync(
              WebSocketCloseStatus.NormalClosure,
              "Cancelled",
              CancellationToken.None
            )
            |> Async.AwaitTask
        else

          let! endOfMessage = writeMemory(ws, pipe.Writer)

          if endOfMessage then
            do! pipe.Writer.CompleteAsync().AsTask() |> Async.AwaitTask
            let! result = readMemory(pipe.Reader)
            result
            pipe.Reset()
          else
            ()

      match ws.State with
      | WebSocketState.Aborted ->
        // Notify that we finished abnormally
        failwith "The connection was closed"
      | _ -> ()

      do! pipe.Writer.CompleteAsync().AsTask() |> Async.AwaitTask
      do! pipe.Reader.CompleteAsync().AsTask() |> Async.AwaitTask
      pipe.Reset()
    }

  let create () =
    { new BskyJetstream with
        member _.toAsyncSeq(uri, ?token) =
          jetstreamAsAsyncSeq(Uri uri, defaultArg token CancellationToken.None)
    }

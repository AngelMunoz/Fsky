[<AutoOpen>]
module Library.Extensions

open System
open System.Text.Json.Nodes
open Avalonia.Controls
open Avalonia.Data
open AsyncImageLoader
open Avalonia.Media.Imaging
open Flurl.Http
open FsToolkit.ErrorHandling

type Image with

  member this.AsyncSource
    (binding: IBinding, ?mode: BindingMode, ?priority: BindingPriority)
    =
    let mode = defaultArg mode BindingMode.TwoWay
    let priority = defaultArg priority BindingPriority.LocalValue

    let descriptor =
      ImageLoader.SourceProperty
        .Bind()
        .WithMode(mode)
        .WithPriority(priority)

    this[descriptor] <- binding
    this

  member this.AsyncSource(url: string) =

    this[ImageLoader.SourceProperty] <- url
    this

  member this.IsLoading
    (binding: IBinding, ?mode: BindingMode, ?priority: BindingPriority)
    =
    let mode = defaultArg mode BindingMode.OneWayToSource
    let priority = defaultArg priority BindingPriority.LocalValue

    let descriptor =
      ImageLoader.IsLoadingProperty
        .Bind()
        .WithMode(mode)
        .WithPriority(priority)

    this[descriptor] <- binding
    this

type AdvancedImage with

  member this.Source(value: string) =
    this[AdvancedImage.SourceProperty] <- value
    this

  member this.Source(value: Uri) =
    this[AdvancedImage.SourceProperty] <- value.ToString()
    this

  member this.Loader(loader: IAsyncImageLoader) =
    this[AdvancedImage.LoaderProperty] <- loader
    this

  member this.WebImageLoader() =
    this.Loader(
      { new IAsyncImageLoader with
          member this.ProvideImageAsync(url) = task {
            try
              let! response = url.GetAsync()
              let! image = response.GetStreamAsync()
              return new Bitmap(image)
            with _ ->
              return null
          }

          member this.Dispose() = ()
      }
    )


module Observable =
  let inline tap ([<InlineIfLambda>] f) (source: IObservable<_>) =
    source
    |> Observable.map(fun v ->
      f v
      v
    )

module JsonNode =
  let getOptJsonObject (name: string) (json: JsonObject) = voption {
    match json.TryGetPropertyValue name with
    | true, value -> return value
    | _ -> return! ValueNone
  }

  let tryGetProperty<'T> (name: string) (object: JsonObject) = result {
    match object.TryGetPropertyValue name with
    | true, did ->
      let value = did.AsValue()

      match value.TryGetValue<'T>() with
      | true, value -> return value
      | false, _ ->
        return!
          Error
            $"Unable to cast '%s{value.ToJsonString()}' as type '%s{typeof<'T>.ToString()}'"
    | false, _ ->
      return!
        Error $"Unable to get '%s{name}' property in %s{object.ToJsonString()}"
  }

module DateTimeOffset =

  let tryOfString (value: string) = voption {
    match DateTimeOffset.TryParse(value) with
    | true, value -> return value
    | false, _ -> return! ValueNone
  }

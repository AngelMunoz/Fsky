[<AutoOpen>]
module Library.Extensions

open System
open System.Text.Json.Nodes
open Avalonia.Controls
open Avalonia.Data
open AsyncImageLoader

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
    this[AdvancedImage.LoaderProperty] <- new Loaders.BaseWebImageLoader()
    this

module JsonNode =
  let inline tryGetProperty (name: string) (json: JsonObject) =
    match json.TryGetPropertyValue name with
    | true, value -> ValueSome value
    | _ -> ValueNone

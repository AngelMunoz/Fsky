[<AutoOpen>]
module Library.Extensions

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

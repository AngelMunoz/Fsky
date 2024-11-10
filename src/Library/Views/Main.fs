module Library.Views.Main

open FSharp.Control.Reactive
open FSharp.Control.Reactive.Builders

open Avalonia.Controls

open NXUI.FSharp.Extensions
open Navs
open Navs.Avalonia

open Library.Env

let Content (router: IRouter<Control>) : Control =
  let counter = Subject.behavior 0

  let counterText = observe {
    let! contents = counter
    $"You clicked {contents} times"
  }

  router.NavigateByName("home")
  |> Async.AwaitTask
  |> Async.Ignore
  |> Async.StartImmediate

  UserControl()
    .content(RouterOutlet().DockTop().router(router))

let Window router =
  Window()
    .minWidth(520)
    .minHeight(520)
    .content(Content router)

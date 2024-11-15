module Library.Views.Main

open FSharp.Control.Reactive
open FSharp.Control.Reactive.Builders

open Avalonia
open Avalonia.Controls
#if DEBUG
open Avalonia.Diagnostics
#endif

open NXUI
open NXUI.Extensions
open Navs
open Navs.Avalonia

open Library.Env

let Content (router: IRouter<Control>) : Control =

  router.NavigateByName("home")
  |> Async.AwaitTask
  |> Async.Ignore
  |> Async.StartImmediate

  UserControl()
    .Content(RouterOutlet().DockTop().router(router))

let Window router =
  let w = 
    Window()
      .MinWidth(520)
      .MinHeight(520)
      .Content(Content router)
  #if DEBUG
  w .AttachDevTools()
  #endif
  w

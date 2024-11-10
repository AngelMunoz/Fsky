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
    .content(
      DockPanel()
        .children(
          StackPanel()
            .OrientationHorizontal()
            .DockTop()
            .children(
              Button()
                .content("Click me!!")
                .OnClickHandler(fun _ _ -> counter.OnNext(counter.Value + 1)),
              TextBlock().text(counterText)
            ),
          RouterOutlet().DockTop().router(router)
        )
    )

let Window router =
  Window()
    .width(420)
    .height(420)
    .content(Content router)

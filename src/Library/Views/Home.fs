module Library.Views.Home

open System
open System.Collections.ObjectModel
open Avalonia
open Avalonia.Data
open Avalonia.Controls
open Avalonia.Controls.Templates
open Avalonia.Animation
open Avalonia.Media

open NXUI
open NXUI.Extensions

open IcedTasks
open IcedTasks.Polyfill.Async
open FSharp.Control.Reactive

open AsyncImageLoader
open Navs
open Library
open Library.Env
open Library.Stores

type private Border with
  member inline this.WithTransitions() =
    this.AttachedToLogicalTree
    |> Observable.add(fun _ -> this.Opacity <- 0.0)

    this.Loaded
    |> Observable.add(fun _ -> this.Opacity <- 1.0)

    this.DetachedFromLogicalTree
    |> Observable.add(fun _ -> this.Opacity <- 0.0)

    let tr = Transitions()

    tr.Add(
      DoubleTransition(
        Property = StackPanel.OpacityProperty,
        Duration = TimeSpan.FromMilliseconds(500),
        Easing = Easings.CubicEaseInOut()
      )
    )


    this.Transitions(tr)

let private PostCard =
  FuncDataTemplate<Post>(
    (fun post _ ->
      let time = post.time.ToString("g")
      let text = post.text
      let avatar = post.avatar
      let displayName = post.displayName
      let handle = $"@{post.handle}"

      let topSection =
        StackPanel()
          .OrientationHorizontal()
          .Spacing(4)
          .VerticalAlignmentBottom()
          .Children(
            Border()
              .CornerRadius(75)
              .ClipToBounds(true)
              .Child(
                AdvancedImage(Unchecked.defaultof<Uri>)
                  .WebImageLoader()
                  .Width(32)
                  .Height(32)
                  .Margin(0, 0, 8, 0)
                  .Source(avatar)
              ),
            TextBlock()
              .VerticalAlignmentBottom()
              .Text(displayName)
              .FontSize(18.0),
            TextBlock()
              .VerticalAlignmentBottom()
              .Text(handle)
              .FontStyle(FontStyle.Italic)
              .Foreground(Brushes.Gray)
              .FontSize(16.0)
          )


      Border()
        .WithTransitions()
        .MaxWidth(630)
        .Padding(8)
        .CornerRadius(4)
        .BorderBrush(Brushes.SlateGray)
        .BorderThickness(1, 0, 0, 2)
        .Margin(0, 0, 0, 8)
        .BoxShadow(BoxShadows(BoxShadow.Parse("10 10 20 0 White")))
        .Child(
          StackPanel()
            .Children(
              topSection,
              TextBlock().Text(time).FontSize(12.0),
              TextBlock()
                .TextWrappingWrap()
                .Text(text)
                .FontSize(14.0)
            )
        )
    ),
    supportsRecycling = true
  )

let inline private postList (addDisposable, posts: ObservableCollection<Post>) =
  ScrollViewer()
    .Content(
      ItemsControl()
        .ItemsSource(posts)
        .ItemTemplate(PostCard)
    )

let inline private loginForm
  (credentials, onHandleChanged, onPasswordChanged, onSubmit)
  =

  let handle = credentials |> Observable.map _.handle
  let password = credentials |> Observable.map _.password

  DockPanel()
    .Margin(0, 0, 4, 0)
    .MaxWidth(420)
    .HorizontalAlignmentCenter()
    .VerticalAlignmentCenter()
    .Children(
      TextBlock()
        .DockTop()
        .Text("Welcome To Fsky, a sample client for Bluesky written in F#")
        .TextWrappingWrap()
        .FontSize(32)
        .TextAlignmentCenter(),
      TextBox()
        .DockTop()
        .Margin(0, 4)
        .Text(handle)
        .Watermark("@handle")
        .OnTextChangedHandler(fun sender _ -> onHandleChanged sender.Text)
        .FontSize(16.0),
      TextBox()
        .DockTop()
        .Margin(0, 4)
        .PasswordChar('*')
        .Watermark("xxxx-xxxx-xxxx-xxxx")
        .FontSize(16.0)
        .Text(password)
        .OnTextChangedHandler(fun sender _ -> onPasswordChanged sender.Text),
      Button()
        .DockTop()
        .Content("Authenticate")
        .OnClickHandler(fun _ _ -> onSubmit())
        .FontSize(16.0)
    )


let view env =
  fun ctx (nav: INavigable<_>) -> async {
    let hs = HomeStore.create(env)
    RouteContext.addDisposable hs ctx
    let! token = Async.CancellationToken

    hs.loadPosts(token)
    let credentials = hs.credentials |> Observable.publish

    let onHandleChanged handle =
      hs.credentials.OnNext {
        handle = handle
        password = hs.credentials.Value.password
      }

    let onPasswordChanged password =
      hs.credentials.OnNext {
        handle = hs.credentials.Value.handle
        password = password
      }

    let onSubmit () =
      let { handle = handle; password = password } = hs.credentials.Value

      printfn $"Login - %s{handle}:%s{password}"

      nav.NavigateByName("timelines")
      |> Async.AwaitTask
      |> Async.Ignore
      |> Async.StartImmediate


    return
      UserControl()
        .OnUnloadedHandler(fun _ _ -> hs.stopPostStream())
        .Content(
          DockPanel()
            .Margin(8)
            .Children(
              loginForm(
                credentials,
                onHandleChanged,
                onPasswordChanged,
                onSubmit
              )
                .DockLeft(),
              postList(ctx.addDisposable, hs.posts)
                .DockRight()
            )
        )
      :> Control
  }

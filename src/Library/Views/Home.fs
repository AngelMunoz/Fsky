module Library.Views.Home

open Avalonia.Controls
open NXUI
open NXUI.Extensions
open NXUI.FSharp.Extensions

open Library.Env

let view (Jetstream js) _ _ : Async<Control> = async {

  return UserControl().content("Home") :> Control
}

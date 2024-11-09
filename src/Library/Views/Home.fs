module Library.Views.Home

open Avalonia.Controls
open NXUI
open NXUI.Extensions
open NXUI.FSharp.Extensions


let view _ _ : Async<Control> = async {

  return UserControl().content("Home") :> Control
}

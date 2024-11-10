module Library.Views.Home

open Avalonia.Controls
open NXUI
open NXUI.Extensions
open NXUI.FSharp.Extensions

open Library.Env
open Library.Stores

let view (hs: HomeStore) _ _ : Async<Control> = async {

  return UserControl().content("Home") :> Control
}

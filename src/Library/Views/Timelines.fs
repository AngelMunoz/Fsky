module Library.Views.Timelines


open Avalonia
open Avalonia.Data
open Avalonia.Controls
open Avalonia.Animation

open NXUI
open NXUI.Extensions
open NXUI.Interactivity

open IcedTasks
open IcedTasks.Polyfill.Async
open FSharp.Control.Reactive

open Library
open Library.Stores
open Avalonia.Controls.Templates
open System
open Avalonia.Media
open System.Collections.ObjectModel

let view _ _ _ : Async<Control> = async {
  return UserControl().Content("Timelines") :> Control
}

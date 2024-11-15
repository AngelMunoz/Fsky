module Library.Services.Auth

open System
open System.Collections.Generic
open System.IO.Pipelines
open System.Net
open System.Net.Http
open System.Net.WebSockets
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization
open System.Threading

open Flurl
open Flurl.Http

open FSharp.Control
open IcedTasks
open IcedTasks.Polyfill.Async

open FsToolkit.ErrorHandling

open Library


type Auth = {
  handle: string
  password: string
  authFactorToken: string option
}

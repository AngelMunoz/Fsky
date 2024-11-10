namespace Library

open System
open System.Threading

open Avalonia.Controls

open Navs
open Library

type Jetstream =
  abstract Jetstream: BskyJetstream

type Env =
  inherit Jetstream


module Env =
  let (|Jetstream|) (env: #Jetstream) = env.Jetstream

  let create () =
    { new Env with
        member _.Jetstream = JetStream.create()
    }

namespace Library

open System
open System.Threading

open Avalonia.Controls

open Navs
open Library
open Library.Services

type Bluesky =
  abstract Bluesky: BskyAPI

type Jetstream =
  abstract Jetstream: BskyJetstream


type Env =
  inherit Jetstream
  inherit Bluesky

module Env =
  let (|Jetstream|) (env: #Jetstream) = env.Jetstream

  let (|Bluesky|) (env: #Bluesky) = env.Bluesky

  let create () =

    { new Env with
        member _.Jetstream = JetStream.create()
        member _.Bluesky = BskyAPI.create(None)
    }

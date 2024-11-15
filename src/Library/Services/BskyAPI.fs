namespace Library.Services

open System
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization

open IcedTasks
open IcedTasks.Polyfill.Async

open Flurl
open Flurl.Http
open FsToolkit.ErrorHandling

type BskyStrongRef = { cid: string; uri: string }

type BskyReplyRef = {
  root: BskyStrongRef
  parent: BskyStrongRef
}

// partial post, we don't need all the fields
type BskFeedPost = {
  text: string
  createdAt: DateTimeOffset
  reply: BskyReplyRef option
  tags: string list option
}

type Commit = {
  rev: string
  operation: string
  collection: string
  rkey: string
  record: BskFeedPost option
  cid: string
}

type Identity = {
  did: string
  handle: string
  seq: int64
  time: string
}

type Account = {
  active: bool
  did: string
  seq: int64
  time: string
}

type Event = {
  did: string
  time_us: int64
  commit: Commit option
  account: Account option
  identity: Identity option
}

type Profile = {
  did: string
  handle: string
  displayName: string
  description: string voption
  avatar: Uri voption
  banner: Uri voption
  followersCount: int
  followsCount: int
  postsCount: int
  createdAt: DateTimeOffset
}

module JsonNode =
  let inline tryGetProperty (name: string) (json: JsonObject) =
    match json.TryGetPropertyValue name with
    | true, value -> ValueSome value
    | _ -> ValueNone



module Profile =

  let ofJsonNode (json: JsonNode) = option {
    let profile = json.AsObject()

    let! did =
      profile |> JsonNode.tryGetProperty "did" |> ValueOption.map(_.GetValue())

    let! handle =
      profile
      |> JsonNode.tryGetProperty "handle"
      |> ValueOption.map(_.GetValue())

    let! displayName =
      profile
      |> JsonNode.tryGetProperty "displayName"
      |> ValueOption.map(_.GetValue())

    let description =
      profile
      |> JsonNode.tryGetProperty "description"
      |> ValueOption.map(_.GetValue())

    let avatar =
      profile
      |> JsonNode.tryGetProperty "avatar"
      |> ValueOption.map(fun v -> v.GetValue() |> Uri)

    let banner =
      profile
      |> JsonNode.tryGetProperty "banner"
      |> ValueOption.map(fun v -> v.GetValue() |> Uri)

    let! followersCount =
      profile
      |> JsonNode.tryGetProperty "followersCount"
      |> ValueOption.map(_.GetValue())

    let! followsCount =
      profile
      |> JsonNode.tryGetProperty "followsCount"
      |> ValueOption.map(_.GetValue())

    let! postsCount =
      profile
      |> JsonNode.tryGetProperty "postsCount"
      |> ValueOption.map(_.GetValue())

    let! createdAt =
      profile
      |> JsonNode.tryGetProperty "createdAt"
      |> ValueOption.bind(fun v ->
        match DateTimeOffset.TryParse(v.GetValue<string>()) with
        | true, value -> ValueSome value
        | _ -> ValueNone
      )

    return {
      did = did
      handle = handle
      displayName = displayName
      description = description
      avatar = avatar
      banner = banner
      followersCount = followersCount
      followsCount = followsCount
      postsCount = postsCount
      createdAt = createdAt
    }

  }

type AuthPayload = {
  handle: string
  password: string
  authFactorToken: string option
}

type CreateSessionResponse = {
  accessJwt: string
  refreshJwt: string
  handle: string
  did: string
  didDoc: obj | null
  email: string | null
  emailConfirmed: Nullable<bool>
  emailAuthFactor: Nullable<bool>
  active: Nullable<bool>
  status: string | null
}


type BskyAPI =
  abstract setBaseUrl : baseUrl: string -> unit
  abstract resolveHandle : did: string -> Async<Profile option>
  abstract login: payload: AuthPayload -> Async<CreateSessionResponse>

module BskyAPI =
  let create(baseUrl: string option) =
    let mutable baseUrl = defaultArg baseUrl "https://public.api.bsky.app/xrpc"

    { new BskyAPI with
      member _.setBaseUrl newUrl = baseUrl <- newUrl

      member _.resolveHandle did = async {
          let! token = Async.CancellationToken

          try
            let! profile =
              $"{baseUrl}/app.bsky.actor.getProfile?actor={did}".GetJsonAsync<JsonNode>(cancellationToken = token)

            return Profile.ofJsonNode profile
          with _ ->
            return None
        }
      member _.login payload = async {
          let! token = Async.CancellationToken
          let! createSessionResult =
            $"{baseUrl}/com.atproto.server.createSession"
              .PostJsonAsync(payload, cancellationToken = token)
          return! createSessionResult.GetJsonAsync<CreateSessionResponse>()
        }
    }

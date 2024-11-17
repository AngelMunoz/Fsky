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

open Library

module JetstreamTypes =
  type BskyStrongRef = { cid: string; uri: string }

  type BskyReplyRef = {
    root: BskyStrongRef
    parent: BskyStrongRef
  }

  // partial post, we don't need all the fields
  type Post = {
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
    record: Post option
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

module AppBskyRichTextFacet =

   type FacetIndex = { byteEnd: int; byteStart: int }
   type FacetFeature = {
     ``$type``: string
     did: string
   }
   type Mention = {
     ``$type``: string
     index: FacetIndex
     features: FacetFeature seq
   }

module AppBskyFeedGetFeed =

  module BlessedAtUriFeeds =
      [<Literal>]
      let WhatsHot = "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/whats-hot"
      [<Literal>]
      let WhatsHotClassic = "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/hot-classic"
      [<Literal>]
      let BskyTeam = "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/bsky-team"

  type PostAuthor = {
    did: string
    handle: string
    displayName: string
    avatar: Uri
    createdAt: DateTimeOffset
  }

  module PostAuthor =

     let ofJsonNode (node: JsonNode) = validation {
       let doc = node.AsObject()

       let! did = JsonNode.tryGetProperty<string> "did" doc
       and! handle = JsonNode.tryGetProperty<string> "handle" doc
       and! displayName = JsonNode.tryGetProperty<string> "displayName" doc
       and! avatar = JsonNode.tryGetProperty<string> "avatar" doc |> Result.map Uri

       let! createdAt = JsonNode.tryGetProperty<string> "createdAt" doc |> Result.map DateTimeOffset.tryOfString
       let! createdAt = createdAt |> Result.requireValueSome "Unable to convert createdAt"

       return {
         did = did
         handle = handle
         displayName = displayName
         avatar = avatar
         createdAt = createdAt
       }
     }


  type PostRecord = {
    ``$type``: string
    createdAt: DateTimeOffset
    langs: string seq
    text: string
  }

  module PostRecord =

    let ofJsonNode (node: JsonNode) = validation {
      let doc = node.AsObject()
      let! type' = JsonNode.tryGetProperty<string> "$type" doc
      and! langs = JsonNode.tryGetPropertySeq<string> "langs" doc
      and! text = JsonNode.tryGetProperty<string> "text" doc

      let! createdAt = JsonNode.tryGetProperty<string> "createdAt" doc |> Result.map DateTimeOffset.tryOfString
      let! createdAt = createdAt |> Result.requireValueSome "Unable to convert createdAt"

      return {
        ``$type`` = type'
        createdAt = createdAt
        langs = langs
        text = text
      }
    }

  type Post = {
    uri: Uri
    cid: string
    author: PostAuthor
    record: PostRecord
    replyCount: int
    repostCount: int
    quoteCount: int
    indexedAt: DateTimeOffset
    feedContext: string
  }

  type Feed = {
    feed: Post seq
    cursor: string
  }

module Profile =

  let ofJsonNode (json: JsonNode) = option {
    let profile = json.AsObject()

    let! did =
      profile |> JsonNode.getOptJsonObject "did" |> ValueOption.map(_.GetValue())

    let! handle =
      profile
      |> JsonNode.getOptJsonObject "handle"
      |> ValueOption.map(_.GetValue())

    let! displayName =
      profile
      |> JsonNode.getOptJsonObject "displayName"
      |> ValueOption.map(_.GetValue())

    let description =
      profile
      |> JsonNode.getOptJsonObject "description"
      |> ValueOption.map(_.GetValue())

    let avatar =
      profile
      |> JsonNode.getOptJsonObject "avatar"
      |> ValueOption.map(fun v -> v.GetValue() |> Uri)

    let banner =
      profile
      |> JsonNode.getOptJsonObject "banner"
      |> ValueOption.map(fun v -> v.GetValue() |> Uri)

    let! followersCount =
      profile
      |> JsonNode.getOptJsonObject "followersCount"
      |> ValueOption.map(_.GetValue())

    let! followsCount =
      profile
      |> JsonNode.getOptJsonObject "followsCount"
      |> ValueOption.map(_.GetValue())

    let! postsCount =
      profile
      |> JsonNode.getOptJsonObject "postsCount"
      |> ValueOption.map(_.GetValue())

    let! createdAt =
      profile
      |> JsonNode.getOptJsonObject "createdAt"
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

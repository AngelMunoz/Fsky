module Library.Services.Feed


open BSky.Api
open BSky.Client
open BSky.Model

type FetchFeed<'T when 'T: (member FetchFeed: unit -> Async<unit>)> = 'T

type FeedServive<'T when FetchFeed<'T>> = 'T

let inline fetchFeed<'T when FetchFeed<'T>> (feed: FeedServive<'T>) =
  feed.FetchFeed()

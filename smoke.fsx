#r "netstandard"
#r @"packages/Expecto/lib/netstandard2.0/Expecto.dll"
#r @"packages/Hopac/lib/netstandard2.0/Hopac.Core.dll"
#r @"packages/Hopac/lib/netstandard2.0/Hopac.dll"
#r @"packages/Http.fs/lib/netstandard2.0/HttpFs.dll"

open System
open Expecto
open Expecto.Flip
open Hopac
open HttpFs.Client

let apiHost = Environment.GetEnvironmentVariable("API_HOST")
let apiPort = Environment.GetEnvironmentVariable("API_PORT")
let dogApiUrl = sprintf "http://%s:%s" apiHost apiPort

let httpPost url body : Job<int * string> =
    let req =
        Request.createUrl Post url
        |> Request.bodyString body
    job {
        use! resp = req |> getResponse
        let! respBody = resp |> Response.readBodyAsString
        let statusCode = resp.statusCode
        do! timeOutMillis 500
        return (statusCode, respBody)
    }

let reverseJob (dogId:Guid) (eventNumber:int) (effectiveDate:DateTime) =
    let body =
        sprintf """
        {
          "dogId": "%s",
          "source": "test",
          "effectiveDate": "%s",
          "eventNumber": %d
        }
        """
        <| dogId.ToString()
        <| effectiveDate.ToString("o")
        <| eventNumber
    let createUrl = sprintf "%s/reverse" dogApiUrl
    httpPost createUrl body

let createJob (dogId:Guid) (name, breed) (effectiveDate:DateTime) =
    let body =
        sprintf """
        {
          "dogId": "%s",
          "source": "test",
          "effectiveDate": "%s",
          "dog": {
            "name": "%s",
            "breed": "%s"
          }
        }
        """
        <| dogId.ToString()
        <| effectiveDate.ToString("o")
        <| name
        <| breed
    let createUrl = sprintf "%s/create" dogApiUrl
    httpPost createUrl body

let commandJob endpoint (dogId:Guid) (effectiveDate:DateTime) =
    let body =
        sprintf """
        {
          "dogId": "%s",
          "source": "test",
          "effectiveDate": "%s"
        }
        """
        <| dogId.ToString()
        <| effectiveDate.ToString("o")
    let commandUrl = sprintf "%s/%s" dogApiUrl endpoint
    httpPost commandUrl body

module Benji =
    let benjiId = Guid.NewGuid()
    let benjiJobs =
        [ DateTime(2019, 1, 1) |> createJob benjiId ("Benji", "Maltipoo")
          DateTime(2019, 1, 2) |> commandJob "eat" benjiId
          DateTime(2019, 1, 3) |> commandJob "play" benjiId
          DateTime(2019, 1, 4) |> commandJob "sleep" benjiId
          DateTime(2019, 1, 5) |> commandJob "wake" benjiId
          // reverse the previous sleep event
          DateTime(2019, 1, 5) |> reverseJob benjiId 3 
          // should fail since dog will not be in correct state
          DateTime(2019, 1, 6) |> commandJob "eat" benjiId
          // add a new sleep event to replace reversed sleep event
          DateTime(2019, 1, 4) |> commandJob "sleep" benjiId
          // retry the eat command
          DateTime(2019, 1, 6) |> commandJob "eat" benjiId
          DateTime(2019, 1, 7) |> commandJob "play" benjiId ]
    let runWorkflow () =
        benjiJobs
        |> Job.seqCollect
        |> run

let testBenji =
    test "test Benji" {
        let expected =
            [ (200, """{"committedEvents":["Born"]}""") 
              (200, """{"committedEvents":["Ate"]}""") 
              (200, """{"committedEvents":["Played"]}""") 
              (200, """{"committedEvents":["Slept"]}""") 
              (200, """{"committedEvents":["Woke"]}""") 
              (200, """{"committedEvents":["Reversed"]}""") 
              (500, "DomainError \"dog cannot eat; dog is Corrupt \"dog cannot wake in state: Tired\"\"") 
              (200, """{"committedEvents":["Slept"]}""") 
              (200, """{"committedEvents":["Ate"]}""") 
              (200, """{"committedEvents":["Played"]}""") ]
        Benji.runWorkflow ()
        |> Expect.sequenceEqual "responses should equal" expected 
    }

runTests defaultConfig testBenji

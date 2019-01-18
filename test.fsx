#r "netstandard"
#r @"packages\Expecto\lib\netstandard2.0\Expecto.dll"
#r @"packages\Hopac\lib\netstandard2.0\Hopac.Core.dll"
#r @"packages\Hopac\lib\netstandard2.0\Hopac.dll"
#r @"packages\Http.fs\lib\netstandard2.0\HttpFs.dll"
#r @"src\Dog\out\Dog.dll"
#r @"src\Dog\out\Ouroboros.dll"

open System
open Expecto
open Hopac
open HttpFs.Client
open Ouroboros
open Dog
open Dog.Implementation

let dogApiUrl = "http://localhost:8080"

let httpPost url body : Job<int * string> =
    let req =
        Request.createUrl Post url
        |> Request.bodyString body
    job {
        use! resp = req |> getResponse
        let! respBody = resp |> Response.readBodyAsString
        let statusCode = resp.statusCode
        return (statusCode, respBody)
    }

let reverseJob (dogId:Guid) (effectiveDate:DateTime) (eventNumber:int) =
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
          DateTime(2018, 1, 2) |> commandJob "eat" benjiId ]
    let runWorkflow () =
        benjiJobs
        |> Job.seqCollect
        |> run

let testBenji =
    test "test Benji" {
        let expected =
            [ (200, "") 
              (200, "")
              (200, "")
              (200, "")
              (200, "")
              (200, "") ]
        let actual = Benji.runWorkflow ()
        Expect.sequenceEqual actual expected "responses should equal"
    }

runTests defaultConfig testBenji

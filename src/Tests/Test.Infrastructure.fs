module Test.Infrastructure

open Ouroboros
open Expecto
open Expecto.Flip

module String =
    let clean (s:string) =
        s.Replace(" ", "").Replace(System.Environment.NewLine, "")

type Dog =
    { Name: string
      Breed: string
      Age: int }

type DogEvent =
    | Born of Dog
    | Ate
    | Slept
    | Woke
    | Played

let benji =
    { Name = "Benji"
      Breed = "Maltipoo"
      Age = 6 }

let born = Born benji

let ate = Ate

[<Tests>]
let testJson =
    testList "test Json" [
        test "record bytes" {
            result {
                let! serialized = Json.serializeToBytes benji
                let! deserialized = Json.deserializeFromBytes<Dog> serialized
                Expect.equal "dogs should equal" benji deserialized
            } |> Expect.isOk "should be ok"
        }
        test "record json" {
            result {
                let json = """
                {
                    "Name": "Benji",
                    "Breed": "Maltipoo",
                    "Age": 6
                }
                """
                let! serialized = Json.serializeToJson benji
                Expect.equal "json should equal" serialized (json |> String.clean)
                let! deserialized = Json.deserializeFromJson<Dog> json
                Expect.equal "dogs should equal" benji deserialized
            } |> Expect.isOk "should be ok"
        }
        test "union record bytes" {
            result {
                let! serialized = Json.serializeToBytes born
                let! deserialized = Json.deserializeFromBytes<DogEvent> serialized
                Expect.equal "events should equal" born deserialized
            } |> Expect.isOk "should be ok"
        }
        test "union record json" {
            result {
                let json = """
                {
                    "Case": "Born", 
                    "Fields": [
                        { 
                            "Name": "Benji", 
                            "Breed": "Maltipoo", 
                            "Age": 6 
                        }
                    ]
                }
                """
                let! serialized = Json.serializeToJson born
                Expect.equal "json should equal" serialized (json |> String.clean)
                let! deserialized = Json.deserializeFromJson<DogEvent> json
                Expect.equal "events should equal" born deserialized
            } |> Expect.isOk "should be ok"
        }
        test "union bytes" {
            result {
                let! serialized = Json.serializeToBytes ate
                let! deserialized = Json.deserializeFromBytes<DogEvent> serialized
                Expect.equal "events should equal" ate deserialized
            } |> Expect.isOk "should be ok"
        }
        test "union json" {
            result {
                let json = """
                { "Case": "Ate" }
                """
                let! serialized = Json.serializeToJson ate
                Expect.equal "json should equal" serialized (json |> String.clean)
                let! deserialized = Json.deserializeFromJson<DogEvent> json
                Expect.equal "events should equal" ate deserialized
            } |> Expect.isOk "should be ok"
        }
    ]

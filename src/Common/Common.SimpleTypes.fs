namespace Common

open System

/// Useful functions for constrained types
module ConstrainedType =

    /// Create a constrained string using the constructor provided
    /// Return Error if input is null, empty, or length > maxLen
    let createString ctor maxLen str = 
        let fieldName = ctor.ToString()
        if String.IsNullOrEmpty(str) then
            sprintf "%s must not be null or empty" fieldName  |> Error
        elif str.Length > maxLen then
            sprintf "%s must not be more than %i chars" fieldName maxLen  |> Error
        else 
            ctor str |> Ok

    /// Create a constrained integer using the constructor provided
    /// Return Error if input is less than minVal or more than maxVal
    let createInt ctor minVal maxVal i = 
        let fieldName = ctor.ToString()
        if i < minVal then
            sprintf "%s: Must not be less than %i" fieldName minVal |> Error
        elif i > maxVal then
            sprintf "%s: Must not be greater than %i" fieldName maxVal |> Error
        else 
            ctor i |> Ok

    /// Create a constrained long using the constructor provided
    /// Return Error if input is less than minVal or more than maxVal
    let createLong ctor (minVal:int64) maxVal i = 
        let fieldName = ctor.ToString()
        if i < minVal then
            sprintf "%s: Must not be less than %i" fieldName minVal |> Error
        elif i > maxVal then
            sprintf "%s: Must not be greater than %i" fieldName maxVal |> Error
        else 
            ctor i |> Ok

    /// Create a constrained decimal using the constructor provided
    /// Return Error if input is less than minVal or more than maxVal
    let createDecimal ctor minVal maxVal d = 
        let fieldName = ctor.ToString()
        if d < minVal then
            sprintf "%s: Must not be less than %M" fieldName minVal |> Error
        elif d > maxVal then
            sprintf "%s: Must not be greater than %M" fieldName maxVal |> Error
        else 
            ctor d |> Ok

    /// Create a constrained decimal<USD> using the constructor provided
    /// Return Error if input is less than minVal or more than maxVal
    let createCurrency<[<Measure>] 'currency,'a> ctor minVal maxVal (d:decimal<'currency>) : Result<'a,string> = 
        let fieldName = ctor.ToString()
        if d < minVal then
            sprintf "%s: Must not be less than %M" fieldName minVal |> Error
        elif d > maxVal then
            sprintf "%s: Must not be greater than %M" fieldName maxVal |> Error
        else 
            ctor d |> Ok

    /// Create a constrained string using the constructor provided
    /// Return Error if input is null. empty, or does not match the regex pattern
    let createLike ctor pattern str = 
        let fieldName = ctor.ToString()
        if String.IsNullOrEmpty(str) then
            sprintf "%s: Must not be null or empty" fieldName |> Error
        elif System.Text.RegularExpressions.Regex.IsMatch(str,pattern) then
            ctor str |> Ok
        else
            sprintf "%s: '%s' must match the pattern '%s'" fieldName str pattern |> Error

/// Constrained to be 50 chars or less, not null
type String50 = private String50 of string
module String50 =
    let value (String50 str) = str
    let create str = 
        ConstrainedType.createString String50 50 str

type PositiveInt = private PositiveInt of int
module PositiveInt =
    let value (PositiveInt value) = value
    let create value =
        ConstrainedType.createInt PositiveInt 0 Int32.MaxValue value
    let zero = PositiveInt 0
    let one = PositiveInt 1
    let max = PositiveInt Int32.MaxValue

type PositiveLong = private PositiveLong of int64
module PositiveLong =
    let value (PositiveLong long) = long
    let create long =
        ConstrainedType.createLong PositiveLong 0L Int64.MaxValue long
    let zero = PositiveLong 0L
    let one = PositiveLong 1L
    let max = PositiveLong Int64.MaxValue

module Guid =
    let toString (guid:Guid) =
        guid.ToString()
    let fromString (s:string) =
        match Guid.TryParse(s) with
        | (true, guid) -> guid |> Ok
        | (false, _) -> sprintf "unable to parse %s" s |> Error

module LLUtils

open System.IO

// Normalizes paths to use the platform-specific directory separator.
let FixPathSeps(path: string) = 
  path.Replace('\\', Path.DirectorySeparatorChar)

// Returns a null-terminated string, read from the specified offset in the file.
let readNullTerminatedString(ba: byte array, encoding: System.Text.Encoding)(pos: int): string option = 
    if (pos >= ba.Length) then
        None
    else
        let mutable endPos = pos
        while (endPos < ba.Length) && (ba.[endPos] <> 0uy) do
            endPos <- endPos + 1

        let endString = 
            if (endPos >= ba.Length) then
                endPos - 1
            else
                endPos

        let result = encoding.GetString(ba, pos, endString - pos)
        Some(result)

// Intended for use with Seq.unfold. Reads all null-terminated strings
// from a byte array.
let readNextStringUnfold(ba: byte array, encoding: System.Text.Encoding)(pos: int): (string * int) option = 
    if (pos >= ba.Length) then
        None
    else
        let mutable endPos = pos + 1
        while (endPos < ba.Length) && (ba.[endPos] <> 0uy) do
            endPos <- endPos + 1

        let endString = 
            if (endPos >= ba.Length) then
                endPos - 1
            else
                endPos

        let decodedString = encoding.GetString(ba, pos, endString - pos)
        Some((decodedString, endPos + 1))


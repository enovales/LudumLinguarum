module LLUtils

open System.IO

// Normalizes paths to use the platform-specific directory separator.
let FixPathSeps(path: string) = 
  path.Replace('\\', Path.DirectorySeparatorChar)

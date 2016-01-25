module InfinityAuroraEnginePlugins.CommonTypes

open System.IO
open System.Text

type ResType = 
    | Res               = 0us
    | Bmp               = 1us
    | Mve               = 2us
    | Tga               = 3us
    | Wav               = 4us
    | Plt               = 6us
    | Ini               = 7us
    | Mp3               = 8us
    | Mpg               = 9us
    | Txt               = 10us
    | Wma               = 11us
    | Wmv               = 12us
    | Xmv               = 13us
    | Plh               = 2000us
    | Tex               = 2001us
    | Mdl               = 2002us
    | Thg               = 2003us
    | Fnt               = 2005us
    | Lua               = 2007us
    | Slt               = 2008us
    | Nss               = 2009us
    | Ncs               = 2010us
    | Mod               = 2011us
    | Are               = 2012us
    | Set               = 2013us
    | Ifo               = 2014us
    | Bic               = 2015us
    | Wok               = 2016us
    | Twoda             = 2017us
    | Tlk               = 2018us
    | Txi               = 2022us
    | Git               = 2023us
    | Bti               = 2024us
    | Uti               = 2025us
    | Btc               = 2026us
    | Utc               = 2027us
    | Dlg               = 2029us
    | Itp               = 2030us
    | Btt               = 2031us
    | Utt               = 2032us
    | Dds               = 2033us
    | Bts               = 2034us
    | Uts               = 2035us
    | Ltr               = 2036us
    | Gff               = 2037us
    | Fac               = 2038us
    | Bte               = 2039us
    | Ute               = 2040us
    | Btd               = 2041us
    | Utd               = 2042us
    | Btp               = 2043us
    | Utp               = 2044us
    | Dft               = 2045us
    | Gic               = 2046us
    | Gui               = 2047us
    | Css               = 2048us
    | Ccs               = 2049us
    | Btm               = 2050us
    | Utm               = 2051us
    | Dwk               = 2052us
    | Pwk               = 2053us
    | Btg               = 2054us
    | Utg               = 2055us
    | Jrl               = 2056us
    | Sav               = 2057us
    | Utw               = 2058us
    | Fourpc            = 2059us
    | Ssf               = 2060us
    | Hak               = 2061us
    | Nwm               = 2062us
    | Bik               = 2063us
    | Ndb               = 2064us
    | Ptm               = 2065us
    | Ptt               = 2066us
    | Lyt               = 3000us
    | Vis               = 3001us
    | Rim               = 3002us
    | Pth               = 3003us
    | Lip               = 3004us
    | Bwm               = 3005us
    | Txb               = 3006us
    | Tpc               = 3007us
    | Mdx               = 3008us
    | Rsv               = 3009us
    | Sig               = 3010us
    | Xbx               = 3011us
    | Erf               = 9997us
    | Bif               = 9998us
    | Key               = 9999us

let ExtensionForResType (rt: ResType) = 
    match rt with
    | ResType.Bmp -> "bmp"
    | ResType.Tga -> "tga"
    | ResType.Wav -> "wav"
    | ResType.Plt -> "plt"
    | ResType.Ini -> "ini"
    | ResType.Txt -> "txt"
    | ResType.Mdl -> "mdl"
    | ResType.Nss -> "nss"
    | ResType.Ncs -> "ncs"
    | ResType.Are -> "are"
    | ResType.Set -> "set"
    | ResType.Ifo -> "ifo"
    | ResType.Bic -> "bic"
    | ResType.Wok -> "wok"
    | ResType.Twoda -> "2da"
    | ResType.Txi -> "txi"
    | ResType.Git -> "git"
    | ResType.Uti -> "uti"
    | ResType.Utc -> "utc"
    | ResType.Dlg -> "dlg"
    | ResType.Itp -> "itp"
    | ResType.Utt -> "utt"
    | ResType.Dds -> "dds"
    | ResType.Uts -> "uts"
    | ResType.Ltr -> "ltr"
    | ResType.Gff -> "gff"
    | ResType.Fac -> "fac"
    | ResType.Ute -> "ute"
    | ResType.Utd -> "utd"
    | ResType.Utp -> "utp"
    | ResType.Dft -> "dft"
    | ResType.Gic -> "gic"
    | ResType.Gui -> "gui"
    | ResType.Utm -> "utm"
    | ResType.Dwk -> "dwk"
    | ResType.Pwk -> "pwk"
    | ResType.Jrl -> "jrl"
    | ResType.Utw -> "utw"
    | ResType.Ssf -> "ssf"
    | ResType.Ndb -> "ndb"
    | ResType.Ptm -> "ptm"
    | ResType.Ptt -> "ptt"
    | _ -> ""

let ResTypeFromFilename(s: string) = 
    match Path.GetExtension(s) with
    | "bmp" -> Some(ResType.Bmp)
    | "tga" -> Some(ResType.Tga)
    | "wav" -> Some(ResType.Wav)
    | "plt" -> Some(ResType.Plt)
    | "ini" -> Some(ResType.Ini)
    | "txt" -> Some(ResType.Txt)
    | "mdl" -> Some(ResType.Mdl)
    | "nss" -> Some(ResType.Nss)
    | "ncs" -> Some(ResType.Ncs)
    | "are" -> Some(ResType.Are)
    | "set" -> Some(ResType.Set)
    | "ifo" -> Some(ResType.Ifo)
    | "bic" -> Some(ResType.Bic)
    | "wok" -> Some(ResType.Wok)
    | "2da" -> Some(ResType.Twoda)
    | "txi" -> Some(ResType.Txi)
    | "git" -> Some(ResType.Git)
    | "uti" -> Some(ResType.Uti)
    | "utc" -> Some(ResType.Utc)
    | "dlg" -> Some(ResType.Dlg)
    | "itp" -> Some(ResType.Itp)
    | "utt" -> Some(ResType.Utt)
    | "dds" -> Some(ResType.Dds)
    | "uts" -> Some(ResType.Uts)
    | "ltr" -> Some(ResType.Ltr)
    | "gff" -> Some(ResType.Gff)
    | "fac" -> Some(ResType.Fac)
    | "ute" -> Some(ResType.Ute)
    | "utd" -> Some(ResType.Utd)
    | "utp" -> Some(ResType.Utp)
    | "dft" -> Some(ResType.Dft)
    | "gic" -> Some(ResType.Gic)
    | "gui" -> Some(ResType.Gui)
    | "utm" -> Some(ResType.Utm)
    | "dwk" -> Some(ResType.Dwk)
    | "pwk" -> Some(ResType.Pwk)
    | "jrl" -> Some(ResType.Jrl)
    | "utw" -> Some(ResType.Utw)
    | "ssf" -> Some(ResType.Ssf)
    | "ndb" -> Some(ResType.Ndb)
    | "ptm" -> Some(ResType.Ptm)
    | "ptt" -> Some(ResType.Ptt)
    | _ -> None

type ResRef = 
    {
        Value: string
    }
    with
        static member FromBinaryReader(r: BinaryReader) = 
            { ResRef.Value = (Encoding.ASCII.GetString(r.ReadBytes(16)).TrimEnd(char 0)) }
    end

type Resource(resref: ResRef, restype: ResType) = 
    class
        member this.ResRef = resref
        member this.ResType = restype
        member this.Filename = resref.Value + "." + ExtensionForResType(restype)
        static member FromBinaryReader(r: BinaryReader) = 
            let resref = ResRef.FromBinaryReader(r)
            let restype = Microsoft.FSharp.Core.LanguagePrimitives.EnumOfValue<uint16, ResType>(r.ReadUInt16())
            new Resource(resref, restype)
    end

type FourCCTuple = char * char * char * char
type FourCC(contents: FourCCTuple) = 
    class
        static member FromBinaryReader(r: BinaryReader) = 
            let chars = Encoding.ASCII.GetChars(r.ReadBytes(4))
            new FourCC((chars.[0], chars.[1], chars.[2], chars.[3]))
    end

type StrRef = uint32
let InvalidStrRef = 0xFFFFFFFFu
let StrRefMask = 0xFF000000u

namespace LudumLinguarumLib

open System.Globalization

type ILocalizedString = 
    abstract member Text: string
    abstract member Culture: CultureInfo

type IGlobalizedString = 
    abstract member Key: string
    abstract member LocalizedStrings: list<ILocalizedString>

type ILocalizedAudio = 
    abstract member SourceString: ILocalizedString
    abstract member Culture: CultureInfo

type IGlobalizedAudio = 
    abstract member Key: string
    abstract member LocalizedAudio: list<ILocalizedAudio>

type GlobalizedResourceType = 
    | Text = 0
    | Audio = 1

type IGlobalizedResource = 
    abstract member Type: GlobalizedResourceType

type IGlobalizedTextResource = 
    interface IGlobalizedResource with
        member this.Type = GlobalizedResourceType.Text

type IGlobalizedAudioResource = 
    interface IGlobalizedResource with
        member this.Type = GlobalizedResourceType.Audio

module InfinityAuroraEnginePlugins.GFFFileTypes

open InfinityAuroraEnginePlugins.CommonTypes
open InfinityAuroraEnginePlugins.GFF
open InfinityAuroraEnginePlugins.SerializedGFF
open InfinityAuroraEnginePlugins.TalkTable

type SerializedSyncStruct = {
        Active: ResRef option
        Index: uint32 option

        // only for non-starting-list sync structs
        IsChild: byte option
        LinkComment: GFFRawCExoString option
    }
    with
        static member FromGFFStructNonStartingList(s: GFFStruct) = {
            SerializedSyncStruct.Active = s.GetResRef("Active");
            Index = s.GetDword("Index");
            IsChild = s.GetByte("IsChild");
            LinkComment = s.GetString("LinkComment")
        }
        static member FromGFFStructStartingList(s: GFFStruct) = {
            SerializedSyncStruct.Active = s.GetResRef("Active");
            Index = s.GetDword("Index");
            IsChild = None;
            LinkComment = None;
        }
    end

type SerializedDialogueStruct = {
        Animation: uint32 option
        AnimLoop: byte option
        Comment: GFFRawCExoString option
        Delay: uint32 option
        EntriesList: List<SerializedSyncStruct> option
        Quest: GFFRawCExoString option
        QuestEntry: uint32 option
        RepliesList: List<SerializedSyncStruct> option
        Script: ResRef option
        Sound: ResRef option
        Speaker: GFFRawCExoString option
        StringRef: uint32 option
        Text: GFFRawCExoLocString option
    }
    with
        static member FromGFFStruct(s: GFFStruct) = {
            SerializedDialogueStruct.Animation = s.GetDword("Animation");
            AnimLoop = s.GetByte("AnimLoop");
            Comment = s.GetString("Comment");
            Delay = s.GetDword("Delay");
            EntriesList = s.GetList("EntriesList") |> Option.map (fun l -> 
                l.Structs |> List.map (fun e -> 
                    SerializedSyncStruct.FromGFFStructNonStartingList(e)));
            Quest = s.GetString("Quest");
            QuestEntry = s.GetDword("QuestEntry");
            RepliesList = s.GetList("RepliesList") |> Option.map (fun l -> 
                l.Structs |> List.map (fun e -> 
                    SerializedSyncStruct.FromGFFStructNonStartingList(e)));
            Script = s.GetResRef("Script");
            Sound = s.GetResRef("Sound");
            Speaker = s.GetString("Speaker");
            StringRef = s.GetStringRef("Text");
            Text = s.GetLocString("Text")
        }
    end

type SerializedDialogue = {
        DelayEntry: uint32 option
        DelayReply: uint32 option
        EndConverAbort: ResRef option
        EndConversation: ResRef option
        EntryList: list<SerializedDialogueStruct> option
        NumWords: uint32 option
        PreventZoomIn: byte option
        ReplyList: list<SerializedDialogueStruct> option
        StartingList: list<SerializedSyncStruct> option
    }
    with
        static member DelayEntryName = "DelayEntry"
        static member DelayReplyName = "DelayReply"
        static member EndConverAbortName = "EndConverAbort"
        static member EndConversationName = "EndConversation"
        static member EntryListName = "EntryList"
        static member NumWordsName = "NumWords"
        static member PreventZoomInName = "PreventZoomIn"
        static member ReplyListName = "ReplyList"
        static member StartingListName = "StartingList"
        static member FromGFF(f: GFF) = 
            {
                SerializedDialogue.DelayEntry = f.Members.GetDword(SerializedDialogue.DelayEntryName);
                DelayReply = f.Members.GetDword(SerializedDialogue.DelayReplyName);
                EndConverAbort = f.Members.GetResRef(SerializedDialogue.EndConverAbortName);
                EndConversation = f.Members.GetResRef(SerializedDialogue.EndConversationName);
                EntryList = f.Members.GetList(SerializedDialogue.EntryListName) |> Option.map (fun e -> e.Structs |> List.map(fun t -> SerializedDialogueStruct.FromGFFStruct(t)));
                NumWords = f.Members.GetDword(SerializedDialogue.NumWordsName);
                PreventZoomIn = f.Members.GetByte(SerializedDialogue.PreventZoomInName);
                ReplyList = f.Members.GetList(SerializedDialogue.ReplyListName) |> Option.map (fun e -> e.Structs |> List.map(fun t -> SerializedDialogueStruct.FromGFFStruct(t)));
                StartingList = f.Members.GetList(SerializedDialogue.StartingListName) |> Option.map (fun e -> e.Structs |> List.map(fun t -> SerializedSyncStruct.FromGFFStructStartingList(t)));
            }
    end

type SyncStructDialogueNode =
    | Node of DialogueStruct
    | Index of uint32
and AugmentedSyncStruct = {
        Active: ResRef option;
        mutable DialogueNode: SyncStructDialogueNode option;
        IsLink: bool;
        LinkComment: GFFRawCExoString option;
    }
    with
        member this.Text: GFFRawCExoLocString option = 
            this.DialogueNode |> Option.bind (fun dn -> 
                match dn with
                |SyncStructDialogueNode.Node n -> n.Text
                | _ -> None)

        static member FromSerialized(s: SerializedSyncStruct) =
            {
                AugmentedSyncStruct.Active = s.Active;
                DialogueNode = s.Index |> Option.map (fun a -> SyncStructDialogueNode.Index a);
                IsLink = match s.IsChild with 
                            | Some(x) when x = (byte 1) -> true
                            | _ -> false;
                LinkComment = s.LinkComment
            }
        member this.Fixup(ds: DialogueStruct array) = 
            match this.DialogueNode with
            | Some(SyncStructDialogueNode.Index i) ->
                this.DialogueNode <- Some(SyncStructDialogueNode.Node ds.[int i])
            | _ -> ()

    end
and DialogueStruct = {
        Animation: uint32 option;
        AnimLoop: byte option;
        Comment: GFFRawCExoString option;
        Delay: uint32 option;
        Quest: GFFRawCExoString option;
        QuestEntry: uint32 option;
        Script: ResRef option;
        Sound: ResRef option;
        Text: GFFRawCExoLocString option;
        Next: List<AugmentedSyncStruct>;
        Speaker: GFFRawCExoString option;
    }
    with
        static member FromSerialized(d: SerializedDialogueStruct, isPlayer: bool) = 
            let nextList = if (isPlayer) then d.EntriesList else d.RepliesList
            let extractedText = 
                match (d.Text, d.StringRef) with
                | (Some(text), _) -> Some(text)
                | (_, Some(strref)) ->
                    // convert to a CExoLocString
                    let locstring = 
                        { 
                            GFFRawCExoLocString.sizeOfOtherData = uint32 0
                            stringRef = strref
                            stringCount = uint32 0
                            substrings = []
                        }
                    Some(locstring)
                | _ -> None

            let retval = {
                DialogueStruct.Animation = d.Animation;
                AnimLoop = d.AnimLoop;
                Comment = d.Comment;
                Delay = d.Delay;
                Quest = d.Quest;
                QuestEntry = d.QuestEntry;
                Script = d.Script;
                Sound = d.Sound;
                Text = extractedText;
                Next = match nextList with 
                       | Some(nl) -> nl |> List.map (fun r -> AugmentedSyncStruct.FromSerialized(r))
                       | _ -> [];
                Speaker = d.Speaker;
            }

            retval

        member self.Fixup(nextList: DialogueStruct array) = 
            self.Next |> List.iter(fun r -> r.Fixup(nextList))

    end
and Dialogue = {
        DelayEntry: uint32 option;
        DelayReply: uint32 option;
        EndConverAbort: ResRef option;
        EndConversation: ResRef option;
        EntryList: DialogueStruct array;
        PreventZoomIn: byte option;
        ReplyList: DialogueStruct array;
        StartingList: AugmentedSyncStruct array;
    }
    with
        static member FromSerialized(d: SerializedDialogue) =
            let entryList = d.EntryList.Value |> List.map (fun r -> DialogueStruct.FromSerialized(r, false)) |> List.toArray
            let replyList = d.ReplyList.Value |> List.map (fun r -> DialogueStruct.FromSerialized(r, true)) |> List.toArray
            let startingList = d.StartingList.Value |> List.map (fun s -> AugmentedSyncStruct.FromSerialized(s)) |> List.toArray

            entryList |> Array.iter (fun e -> e.Fixup(replyList))
            replyList |> Array.iter (fun e -> e.Fixup(entryList))
            startingList |> Array.iter (fun e -> e.Fixup(entryList))

            {
                Dialogue.DelayEntry = d.DelayEntry;
                DelayReply = d.DelayReply;
                EndConverAbort = d.EndConverAbort;
                EntryList = entryList
                EndConversation = d.EndConversation;
                PreventZoomIn = d.PreventZoomIn;
                ReplyList = replyList
                StartingList = startingList
            }
    end

let EvaluateString<'T when 'T :> ITalkTableString>(s: GFFRawCExoLocString, tMasculine: ITalkTable<'T>, tFeminine: ITalkTable<'T>, l: LanguageType, g: Gender): string option = 
    if (s.stringCount > 0u) then
        s.substrings |> List.tryFind(fun t -> (t.languageAndGender.Language = l) && (t.languageAndGender.Gender = g)) |> Option.map (fun t -> t.value)
    else
        match g with
        | Gender.MasculineOrNeutral when uint32 tMasculine.Strings.Length > s.stringRef -> 
            Some((tMasculine.Strings.[int s.stringRef] :> ITalkTableString).Value)
        | Gender.Feminine when uint32 tFeminine.Strings.Length > s.stringRef -> 
            Some((tFeminine.Strings.[int s.stringRef] :> ITalkTableString).Value)
        | _ -> None

let SyncStructString(s: AugmentedSyncStruct, tMasculine: TalkTableV3, tFeminine: TalkTableV3, l: LanguageType, g: Gender): string option = 
    match s.Text with
    | Some(text) -> EvaluateString(text, tMasculine, tFeminine, l, g)
    | _ -> None

let dialogueNodeKey(depth, slot) = "depth " + depth.ToString() + " slot " + slot.ToString()

let rec GatherNonLinkNodesWithText(acc: AugmentedSyncStruct list, n: AugmentedSyncStruct): AugmentedSyncStruct list = 
    match n.IsLink with
    | true -> acc
    | false -> 
        match n.DialogueNode with
        | Some(SyncStructDialogueNode.Node dn) when n.Text.IsSome -> 
            n :: (dn.Next |> List.mapi(fun i next -> GatherNonLinkNodesWithText(acc, next)) |> List.concat)
        | Some(SyncStructDialogueNode.Node dn) -> 
            dn.Next |> List.mapi (fun i next -> GatherNonLinkNodesWithText(acc, next)) |> List.concat
        | _ -> acc

let gatherStringForSyncStruct(i: int)(n: AugmentedSyncStruct) = 
    match n.DialogueNode with
    | Some(SyncStructDialogueNode.Node dn) -> 
        (n.Text.Value, i.ToString())
    | _ -> failwith "should not be called with non dialogue nodes"

let GatherStrings(n: AugmentedSyncStruct): (GFFRawCExoLocString * string) list = 
    let nodes = GatherNonLinkNodesWithText([], n)
    nodes |> List.mapi gatherStringForSyncStruct

let ExtractStringsFromDialogue<'T when 'T :> ITalkTableString>(dialogue: Dialogue, l: LanguageType, g: Gender, maleOrNeuterTalkTable: ITalkTable<'T>, femaleTalkTable: ITalkTable<'T>) =
    let strings = 
        dialogue.StartingList 
        |> Array.map GatherStrings 
        |> List.concat
    strings |> 
        List.map (fun (t, k) -> (EvaluateString(t, maleOrNeuterTalkTable, femaleTalkTable, l, g), k)) |> 
        List.filter(fun (t, k) -> t.IsSome) |> List.map (fun (t, k) -> (t.Value, k))

/// <summary>
/// Utility function to augment the extracted strings and keys with extra information about the dialogue
/// resource and resource container from which the dialogue came.
/// </summary>
/// <param name="sk">tuples of strings and keys</param>
/// <param name="dialogueResref">resref of the dialogue from which this data came</param>
/// <param name="containerString">string describing the resource container (KEY/BIF, ERF, or override) containing this dialogue</param>
let AugmentExtractedStringKeys(sk: (string * string) seq, dialogueResref: ResRef, containerString: string, 
                               gender: Gender) = 
    sk |> Seq.map (fun (s, k) -> 
        let genderedKey = "c " + containerString + " d " + dialogueResref.Value + " g " + gender.ToString() + " " + k
        let genderlessKey = "c " + containerString + " d " + dialogueResref.Value + " " + k
        (s, genderedKey, genderlessKey, gender.ToString()))

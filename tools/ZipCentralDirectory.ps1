# ZIP32 central-directory inspection for the small, deterministic release bundle.
# The build refuses ZIP64 or multi-disk archives instead of guessing offsets.
function Get-ZipCentralEntries {
    param([Parameter(Mandatory)][byte[]]$Bytes)
    if ($Bytes.Length -lt 22) { throw 'Release ZIP has no end record' }
    $End = -1
    $Minimum = [Math]::Max(0, $Bytes.Length - 65557)
    for ($Index = $Bytes.Length - 22; $Index -ge $Minimum; --$Index) {
        if ([BitConverter]::ToUInt32($Bytes, $Index) -ne 0x06054b50) { continue }
        if ($Index + 22 + [BitConverter]::ToUInt16($Bytes, $Index + 20) -ne $Bytes.Length) { continue }
        $End = $Index
        break
    }
    if ($End -lt 0) { throw 'Release ZIP end record is missing or malformed' }
    $Disk = [BitConverter]::ToUInt16($Bytes, $End + 4)
    $DirectoryDisk = [BitConverter]::ToUInt16($Bytes, $End + 6)
    $OnDisk = [BitConverter]::ToUInt16($Bytes, $End + 8)
    $Count = [BitConverter]::ToUInt16($Bytes, $End + 10)
    $Size = [BitConverter]::ToUInt32($Bytes, $End + 12)
    $Offset = [BitConverter]::ToUInt32($Bytes, $End + 16)
    if ($Disk -ne 0 -or $DirectoryDisk -ne 0 -or $OnDisk -ne $Count -or
        $Count -eq [UInt16]::MaxValue -or $Size -eq [UInt32]::MaxValue -or
        $Offset -eq [UInt32]::MaxValue -or [ulong]$Offset + $Size -ne [ulong]$End) {
        throw 'Release ZIP is multi-disk, ZIP64, or has an invalid central directory'
    }
    $Cursor = [int]$Offset
    $Entries = [Collections.Generic.List[object]]::new()
    for ($EntryIndex = 0; $EntryIndex -lt $Count; ++$EntryIndex) {
        if ($Cursor + 46 -gt $End -or [BitConverter]::ToUInt32($Bytes, $Cursor) -ne 0x02014b50) {
            throw 'Release ZIP central entry is malformed'
        }
        $NameLength = [BitConverter]::ToUInt16($Bytes, $Cursor + 28)
        $ExtraLength = [BitConverter]::ToUInt16($Bytes, $Cursor + 30)
        $CommentLength = [BitConverter]::ToUInt16($Bytes, $Cursor + 32)
        $Next = $Cursor + 46 + $NameLength + $ExtraLength + $CommentLength
        if ($Next -gt $End) { throw 'Release ZIP central entry exceeds directory' }
        $Name = [Text.Encoding]::UTF8.GetString($Bytes, $Cursor + 46, $NameLength)
        $Entries.Add([pscustomobject]@{
            Name = $Name
            Offset = $Cursor
            CreatorSystem = [int]$Bytes[$Cursor + 5]
            ExternalAttributes = [BitConverter]::ToUInt32($Bytes, $Cursor + 38)
        })
        $Cursor = $Next
    }
    if ($Cursor -ne $End) { throw 'Release ZIP central directory size does not match entries' }
    return $Entries
}

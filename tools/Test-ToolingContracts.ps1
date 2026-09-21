# Requires PowerShell 7. JSON Schema tests validate the contract seam, not runtime API coverage.
$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$Schema = Join-Path $Root 'api/carbonluau-api.schema.json'
$Availability = @{ SinceApi = 'test-only'; Implemented = $true; Qualification = 'Experimental' }
$Catalog = @{
    SchemaVersion = 1
    Api = @{ Name = 'CarbonLuau'; Version = 'test-only'; Status = 'Experimental' }
    Types = @(@{ Id = 'Test.Service'; Name = 'Test'; Kind = 'Service'; Summary = 'Schema fixture only.';
        Availability = $Availability; Preview = 'Pure' })
    Members = @(
        @{ Id = 'Test.Method'; OwnerId = 'Test.Service'; Name = 'Method'; Kind = 'Method';
            Summary = 'Fixture method.'; Availability = $Availability; Preview = 'Pure';
            Signatures = @(@{ Parameters = @(@{ Name = 'Value'; Type = 'string'; Optional = $false; Variadic = $false }); Returns = @('boolean') }) },
        @{ Id = 'Test.Property'; OwnerId = 'Test.Service'; Name = 'Property'; Kind = 'Property';
            Summary = 'Fixture property.'; Availability = $Availability; Preview = 'Pure';
            ValueType = 'number'; Writable = $false }
    )
    LimitKeys = @('Test.Limit')
}
function Test-Catalog {
    param($Value)
    return Test-Json -Json ($Value | ConvertTo-Json -Depth 30) -SchemaFile $Schema -ErrorAction SilentlyContinue
}
if (!(Test-Catalog $Catalog)) { throw 'Valid API schema fixture rejected' }
$Catalog.SchemaVersion = 2
if (Test-Catalog $Catalog) { throw 'Unknown API schema accepted' }
$Catalog.SchemaVersion = 1
$Catalog.Members[0].Remove('Signatures')
if (Test-Catalog $Catalog) { throw 'Method without signature accepted' }
$Catalog.Members = @($Catalog.Members[1])
$Catalog.Members[0].Remove('ValueType')
if (Test-Catalog $Catalog) { throw 'Property without type accepted' }
$Catalog.Members[0].ValueType = 'number'
$Catalog.Members[0].Availability.Qualification = 'Invented'
if (Test-Catalog $Catalog) { throw 'Unknown qualification accepted' }
$Catalog.Members[0].Availability.Qualification = 'Experimental'
$Catalog.Members[0].RuntimeLimit = 123
if (Test-Catalog $Catalog) { throw 'Undeclared numeric policy accepted in catalog' }
Write-Output '[CarbonLuau:ToolingContractTest] PASS: API schema positive/negative fixtures; runtime coverage/generation deferred to Tooling A'

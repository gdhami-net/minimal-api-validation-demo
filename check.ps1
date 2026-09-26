# Runs the suite that backs the post's claims. Exits non-zero if any fail.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'minimal-api-validation-demo - checking the post''s claims'
Write-Host '  WhatIsCheckedTests      body, query, route and header parameters, nested objects,'
Write-Host '                          List<T> items, [AsParameters], IValidatableObject, a custom'
Write-Host '                          attribute, a form-bound record, and the exact error keys'
Write-Host '  WhatIsSkippedTests      an array property, a dictionary property, a null-bound parameter,'
Write-Host '                          [Required] on a non-nullable int, a class-library endpoint,'
Write-Host '                          IValidatableObject order, DisableValidation(), and key naming'
Write-Host '  ProblemShapeTests       the 400 body with and without AddProblemDetails()'
Write-Host '  NoValidationTests       the same app with the AddValidation() call skipped'
Write-Host '  DepthAndGeneratorTests  the MaxDepth 32 ceiling and a positional record''s attributes'
Write-Host '  ExperimentalApiTests    which parts of the API are stable and which are ASP0029'
Write-Host ''

dotnet test --logger 'console;verbosity=normal'
exit $LASTEXITCODE

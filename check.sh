#!/usr/bin/env bash
# Runs the suite that backs the post's claims. Exits non-zero if any fail.
set -euo pipefail
cd "$(dirname "$0")"

echo "minimal-api-validation-demo — checking the post's claims"
echo "  WhatIsCheckedTests      body, query, route and header parameters, nested objects,"
echo "                          List<T> items, [AsParameters], IValidatableObject, a custom"
echo "                          attribute, a form-bound record, and the exact error keys"
echo "  WhatIsSkippedTests      an array property, a dictionary property, a null-bound parameter,"
echo "                          [Required] on a non-nullable int, a class-library endpoint,"
echo "                          IValidatableObject order, DisableValidation(), and key naming"
echo "  ProblemShapeTests       the 400 body with and without AddProblemDetails()"
echo "  NoValidationTests       the same app with the AddValidation() call skipped"
echo "  DepthAndGeneratorTests  the MaxDepth 32 ceiling and a positional record's attributes"
echo "  ExperimentalApiTests    which parts of the API are stable and which are ASP0029"
echo

dotnet test --logger "console;verbosity=normal"

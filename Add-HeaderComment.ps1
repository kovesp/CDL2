<#
.SYNOPSIS
   Generate and insert header comments for all .cs and .xaml files in a specified directory.
#>

# cspell:ignore Böhringer Dehotay Feuerhahn Köves

[string]$autoGen = 'auto-gen' # Auto-generated header marker. Cannot use 'auto-generated' as it is a reserved in C#.
[string]$HeaderComment = @"
// <$autogen>
//=======================================================================
// <copyright file="{0}" company="Peter Köves">
//     Copyright (c) Peter Köves, 2025. All rights reserved.
//     Licensed under the MIT License. See _LICENSE file in the project root
//     for full license information.
// </copyright>
//=======================================================================
// <author>Peter Köves</author>
// <creation-date>{1}</creation-date>
// 
// <summary>
//   Content description goes here.
// </summary>
// <attribution>
//   This file is part of the clean room reimplementation of the
//      CDL2 Compiler
//      CDL2 Laboratory
//      CDL2 Target Code Generators
//
//    Based on original work on CDL and CDL2 led by C. H. A. Koster
//    and the CDL2 team at the Universities of Berlin, Germany and
//    Nijmegen, The Netherlands.
//
//    The CDL2 Laboratory was the work of Epsilon GmbH, Berlin.
//    H. M. Stahl, H. Feuerhahn, JP. Dehotay, B. Böhringer
//    (and others I don't remember ... sorry).
//
//    This project is not affiliated with the original CDL2 project.
// </attribution>
//=======================================================================
// </$autogen>


"@

[string]$rootDir = 'C:\Users\koves\source\repos\CDL2'
Set-Location -Path $rootDir
[string]$sourceDir = "$rootDir\CDL2v1"
foreach ($source in Get-ChildItem -Path $sourceDir | Where-Object { $_.Extension -in '.cs','.xaml' -or $_.Name -like '*.xaml.cs' } ) {
   $relativePath = $source.FullName.Substring($rootDir.Length + 1)
   $relativePath = $relativePath.Replace('\', '/')
   # Check if the file already has a header comment
   $hasHeader = git show HEAD:"$relativePath" | Select-String -Pattern "<$autogen>"
   if (!$hasHeader) {
      Write-Host -ForegroundColor Green "Processing: $relativePath"
      # Add the header comment to the file
      $creationDate = git log --diff-filter=A --format="%ad" --date=short -- "$relativePath"
      $formattedComment = $HeaderComment -f [System.IO.Path]::GetFileName($relativePath), $creationDate
      if ($source.Extension -eq '.xaml') {
         # For XAML files, need to enclose the header in XML comments
         $formattedComment = "<!--`n" + $formattedComment + "`n-->`n"
      }
      
      # Get the current file content
      $fileContent = Get-Content -Path $source.FullName -Raw -Encoding UTF8
      
      # Combine header comment and file content (header first)
      $newContent = $formattedComment + $fileContent
      
      # Write the combined content to the file
      Set-Content -Path "$relativePath" -Value $newContent -Encoding utf8
   } else {
      Write-Host -ForegroundColor DarkGreen "Skipping:   $relativePath (header already exists)"
   }
}

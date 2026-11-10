# Updates the table of contents in the tutorial .docx and exports it to PDF using Microsoft Word.
# Usage (from the repository root): powershell -ExecutionPolicy Bypass -File docs/tutorial/tools/export_pdf.ps1
$ErrorActionPreference = 'Stop'
$dir  = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$docx = Join-Path $dir 'Kiro-Legacy-Modernization-Tutorial.docx'
$pdf  = Join-Path $dir 'Kiro-Legacy-Modernization-Tutorial.pdf'

$word = New-Object -ComObject Word.Application
$word.Visible = $false
try {
    $doc = $word.Documents.Open($docx)
    foreach ($t in $doc.TablesOfContents) { $t.Update() }
    $doc.Fields.Update() | Out-Null
    $doc.Save()
    $doc.ExportAsFixedFormat([string]$pdf, 17)   # 17 = wdExportFormatPDF
    $doc.Close()
    Write-Host "Wrote $pdf"
}
finally {
    $word.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($word) | Out-Null
}

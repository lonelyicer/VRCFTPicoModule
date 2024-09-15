# Define the file path
$jsonFilePath = Join-Path $env:APPDATA "PICO Connect\settings.json"

# Check if the JSON file exists
if (Test-Path $jsonFilePath) {
    # Prompt for the new value
    $newValue = Read-Host -Prompt 'Enter the new value for faceTrackingTransferProtocol (default is 2)'

    # Set the default value if no input is given
    if (-not $newValue) {
        $newValue = 2
    }

    # Read the JSON file
    $jsonContent = Get-Content -Raw -Path $jsonFilePath | ConvertFrom-Json

    # Modify the value of faceTrackingTransferProtocol
    $jsonContent.lab.faceTrackingTransferProtocol = [int]$newValue

    # Convert the modified object back to JSON
    $modifiedJson = $jsonContent | ConvertTo-Json -Depth 32

    # Write the updated JSON back to the file
    Set-Content -Path $jsonFilePath -Value $modifiedJson

    Write-Host "faceTrackingTransferProtocol has been updated to $newValue successfully."
} else {
    Write-Host "File not found: $jsonFilePath"
}

# Pause and wait for user input before exiting
Read-Host -Prompt "Press any key to exit"
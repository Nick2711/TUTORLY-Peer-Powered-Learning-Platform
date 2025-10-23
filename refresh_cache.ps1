# Script to refresh the chatbot cache
Write-Host "Refreshing chatbot cache..."

try {
    # Get the access token from localStorage (you'll need to replace this with actual token)
    $token = "YOUR_ACCESS_TOKEN_HERE"
    
    # Call the refresh cache endpoint
    $headers = @{
        "Content-Type" = "application/json"
        "Authorization" = "Bearer $token"
    }
    
    $response = Invoke-RestMethod -Uri "http://localhost:5000/api/chatbot/refresh-cache" -Method POST -Headers $headers
    
    Write-Host "Cache refresh response:"
    Write-Host $response | ConvertTo-Json -Depth 3
}
catch {
    Write-Host "Error refreshing cache: $($_.Exception.Message)"
    Write-Host "Make sure the application is running and you have a valid access token"
}

Write-Host "Cache refresh completed."

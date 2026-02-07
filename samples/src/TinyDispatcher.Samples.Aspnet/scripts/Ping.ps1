Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5073/ping" `
  -Headers @{ "X-Request-Id" = "rid_manual_001" } `
  -ContentType "application/json" `
  -Body '{ "message": "hello from PowerShell" }'

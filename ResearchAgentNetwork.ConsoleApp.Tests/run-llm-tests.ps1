# LLM Integration Tests Runner
# This script runs the TaskAnalyzerAgent LLM integration tests

Write-Host "🧪 Running TaskAnalyzerAgent LLM Integration Tests..." -ForegroundColor Cyan

# Check if Ollama is available
Write-Host "🔍 Checking Ollama availability..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -TimeoutSec 5
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ Ollama is available" -ForegroundColor Green
    } else {
        Write-Host "❌ Ollama is not responding properly" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Ollama is not available. Please ensure Ollama is running on http://localhost:11434" -ForegroundColor Red
    Write-Host "💡 To start Ollama, run: ollama serve" -ForegroundColor Yellow
    exit 1
}

# Run the LLM integration tests
Write-Host "🚀 Running tests..." -ForegroundColor Yellow
dotnet test --filter "TaskAnalyzerAgentLLMTests" --verbosity normal

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ All LLM integration tests passed!" -ForegroundColor Green
} else {
    Write-Host "❌ Some tests failed. Check the output above for details." -ForegroundColor Red
}

Write-Host "📊 Test Summary:" -ForegroundColor Cyan
Write-Host "   - Tests require Ollama to be running" -ForegroundColor White
Write-Host "   - Tests will be skipped if Ollama is unavailable" -ForegroundColor White
Write-Host "   - Configuration: appsettings.test.json" -ForegroundColor White 
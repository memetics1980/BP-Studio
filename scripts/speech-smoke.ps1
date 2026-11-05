param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
$english = $speaker.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -like 'en-*' } | Select-Object -First 1
if ($english) { $speaker.SelectVoice($english.VoiceInfo.Name) }
$speaker.SetOutputToWaveFile($OutputPath)
$speaker.Speak('Madam Speaker, we believe that public transport should be free. This policy would help students and low income families travel to school and work. It would also reduce traffic congestion in our cities.')
$speaker.Dispose()

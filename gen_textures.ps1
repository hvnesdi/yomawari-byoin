Add-Type -AssemblyName System.Drawing
$SIZE = 2048
$OUT = "C:\Users\hvnes\YomawariByoin\Assets\Textures"
if (-not (Test-Path $OUT)) { New-Item -ItemType Directory -Path $OUT -Force | Out-Null }
$rng = New-Object System.Random 42

# ---- PatientRoom_Wall_Diffuse ----
Write-Host "Wall..." -NoNewline
$bmp = New-Object System.Drawing.Bitmap($SIZE, $SIZE)
$gr = [System.Drawing.Graphics]::FromImage($bmp)
$gr.Clear([System.Drawing.Color]::FromArgb(220,218,210))
for ($y2 = $SIZE/2; $y2 -lt $SIZE; $y2++) {
    $alpha = [int](35 * ($y2 - $SIZE/2) / ($SIZE/2))
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($alpha,255,220,160))
    $gr.FillRectangle($br, 0, $y2, $SIZE, 1)
    $br.Dispose()
}
for ($i = 0; $i -lt 15; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE)
    $w = $rng.Next(30,200); $h = $rng.Next(5,30)
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180,185,178,165))
    $gr.FillEllipse($br, $x, $y2, $w, $h); $br.Dispose()
}
for ($i = 0; $i -lt 40; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE)
    $x2 = $x + $rng.Next(-100,100); $y3 = $y2 + $rng.Next(-15,15)
    $pn = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120,185,180,170), $rng.Next(1,3))
    $gr.DrawLine($pn, $x, $y2, $x2, $y3); $pn.Dispose()
}
for ($i = 0; $i -lt 20; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE); $r = $rng.Next(20,100)
    $al = $rng.Next(30,70)
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($al,145,135,110))
    $gr.FillEllipse($br, $x-$r, $y2-$r, $r*2, $r*2); $br.Dispose()
}
for ($i = 0; $i -lt 800; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE); $s = $rng.Next(4,16)
    $al = $rng.Next(5,20)
    $dv = $rng.Next(0,3)
    $rv = 220 + $rng.Next(-12,12); $gv = 218 + $rng.Next(-12,12); $bv = 210 + $rng.Next(-12,12)
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($al,$rv,$gv,$bv))
    $gr.FillRectangle($br, $x, $y2, $s, $s); $br.Dispose()
}
$gr.Dispose()
$bmp.Save("$OUT\PatientRoom_Wall_Diffuse.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); Write-Host " done"

# ---- Corridor_Wall_Diffuse ----
Write-Host "Corridor Wall..." -NoNewline
$bmp = New-Object System.Drawing.Bitmap($SIZE, $SIZE)
$gr = [System.Drawing.Graphics]::FromImage($bmp)
$gr.Clear([System.Drawing.Color]::FromArgb(230,225,200))
$wY = [int]($SIZE * 2 / 3)
$br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150,170,150))
$gr.FillRectangle($br, 0, $wY, $SIZE, $SIZE - $wY); $br.Dispose()
$br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(210,210,190))
$gr.FillRectangle($br, 0, $wY-8, $SIZE, 16); $br.Dispose()
for ($i = 0; $i -lt 30; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE); $r = $rng.Next(15,80)
    $al = $rng.Next(25,65)
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($al,185,178,155))
    $gr.FillEllipse($br, $x-$r, $y2-$r, $r*2, $r*2); $br.Dispose()
}
for ($i = 0; $i -lt 600; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE); $s = $rng.Next(4,14)
    $al = $rng.Next(5,18)
    $rv = 230 + $rng.Next(-10,10); $gv = 225 + $rng.Next(-10,10); $bv = 200 + $rng.Next(-10,10)
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($al,$rv,$gv,$bv))
    $gr.FillRectangle($br, $x, $y2, $s, $s); $br.Dispose()
}
$gr.Dispose()
$bmp.Save("$OUT\Corridor_Wall_Diffuse.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); Write-Host " done"

# ---- Floor_Linoleum_Diffuse ----
Write-Host "Floor..." -NoNewline
$bmp = New-Object System.Drawing.Bitmap($SIZE, $SIZE)
$gr = [System.Drawing.Graphics]::FromImage($bmp)
$gr.Clear([System.Drawing.Color]::FromArgb(180,170,150))
$pn = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200,135,125,110), 2)
for ($x = 0; $x -lt $SIZE; $x += 120) { $gr.DrawLine($pn, $x, 0, $x, $SIZE) }
for ($y2 = 0; $y2 -lt $SIZE; $y2 += 120) { $gr.DrawLine($pn, 0, $y2, $SIZE, $y2) }
$pn.Dispose()
for ($i = 0; $i -lt 35; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE); $r = $rng.Next(15,80)
    $al = $rng.Next(30,70)
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($al,140,130,110))
    $gr.FillEllipse($br, $x-$r, $y2-$r, $r*2, $r*2); $br.Dispose()
}
for ($i = 0; $i -lt 800; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE); $s = $rng.Next(4,16)
    $al = $rng.Next(5,22)
    $rv = 180 + $rng.Next(-14,14); $gv = 170 + $rng.Next(-14,14); $bv = 150 + $rng.Next(-14,14)
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($al,$rv,$gv,$bv))
    $gr.FillRectangle($br, $x, $y2, $s, $s); $br.Dispose()
}
$gr.Dispose()
$bmp.Save("$OUT\Floor_Linoleum_Diffuse.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); Write-Host " done"

# ---- Ceiling_Diffuse ----
Write-Host "Ceiling..." -NoNewline
$bmp = New-Object System.Drawing.Bitmap($SIZE, $SIZE)
$gr = [System.Drawing.Graphics]::FromImage($bmp)
$gr.Clear([System.Drawing.Color]::FromArgb(245,243,238))
$pn = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(140,205,203,195), 2)
for ($x = 0; $x -lt $SIZE; $x += 256) { $gr.DrawLine($pn, $x, 0, $x, $SIZE) }
for ($y2 = 0; $y2 -lt $SIZE; $y2 += 256) { $gr.DrawLine($pn, 0, $y2, $SIZE, $y2) }
$pn.Dispose()
for ($i = 0; $i -lt 15; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE); $r = $rng.Next(30,150)
    $al = $rng.Next(40,90)
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($al,175,155,120))
    $gr.FillEllipse($br, $x-$r, $y2-$r, $r*2, $r*2); $br.Dispose()
}
for ($i = 0; $i -lt 20; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE); $r = $rng.Next(10,50)
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(50,75,80,70))
    $gr.FillEllipse($br, $x-$r, $y2-$r, $r*2, $r*2); $br.Dispose()
}
for ($i = 0; $i -lt 600; $i++) {
    $x = $rng.Next(0,$SIZE); $y2 = $rng.Next(0,$SIZE); $s = $rng.Next(4,12)
    $al = $rng.Next(4,15)
    $rv = 245 + $rng.Next(-8,8); $gv = 243 + $rng.Next(-8,8); $bv = 238 + $rng.Next(-8,8)
    $rv = [Math]::Max(0,[Math]::Min(255,$rv)); $gv = [Math]::Max(0,[Math]::Min(255,$gv)); $bv = [Math]::Max(0,[Math]::Min(255,$bv))
    $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($al,$rv,$gv,$bv))
    $gr.FillRectangle($br, $x, $y2, $s, $s); $br.Dispose()
}
$gr.Dispose()
$bmp.Save("$OUT\Ceiling_Diffuse.png", [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); Write-Host " done"

Write-Host "All textures generated!"

@echo off
echo === Testing Report Generator Client ===
echo.

REM הגדרת נתיבים
set EXE_PATH=%~dp0bin\Debug\RunApiAndSaveReport.exe
set OUTPUT_PATH=C:\Temp\Reports

echo בודק אם הקובץ קיים...
if not exist "%EXE_PATH%" (
    echo שגיאה: הקובץ %EXE_PATH% לא נמצא!
    echo אנא build את הפרויקט תחילה.
    pause
    exit /b 1
)

echo יוצר תיקיית פלט...
if not exist "%OUTPUT_PATH%" mkdir "%OUTPUT_PATH%"

echo.
echo === בדיקה 1: דוח פשוט ===
echo קורא: ArnSummaryReport PDF %OUTPUT_PATH%
"%EXE_PATH%" ArnSummaryReport PDF "%OUTPUT_PATH%"

echo.
echo === בדיקה 2: דוח עם פרמטרים ===
echo קורא: ArnSummaryReport Excel %OUTPUT_PATH% mnt=3,isvkod=1  
"%EXE_PATH%" ArnSummaryReport Excel "%OUTPUT_PATH%" "mnt=3,isvkod=1"

echo.
echo === בדיקה 3: מצב אינטראקטיבי ===
echo עבור בדיקה אינטראקטיבית, הרץ:
echo "%EXE_PATH%"
echo.

echo בדיקות הושלמו!
echo קבצי הפלט נמצאים ב: %OUTPUT_PATH%
pause

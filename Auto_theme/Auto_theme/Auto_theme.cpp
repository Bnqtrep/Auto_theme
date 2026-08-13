#include <windows.h>
#include <winreg.h>
#include <shellapi.h>
#include <string>
#include <cmath>

// Windows Runtime headers (C++/CX)
#include <windows.foundation.h>
#include <windows.devices.geolocation.h>
using namespace Windows::Devices::Geolocation;
using namespace Windows::Foundation;

#pragma comment(lib, "shell32.lib")

// ========== Constants ==========
#define WM_TRAYICON (WM_USER + 1)
#define WM_GEO_UPDATE (WM_USER + 2)   // Custom message: geolocation result received
#define ID_TRAY_EXIT   1001
#define ID_TRAY_TOGGLE 1002
#define ID_TRAY_REFRESH 1003
#define TIMER_SWITCH   1

NOTIFYICONDATAW nid = {};
double g_latitude = 35.0;    // Default fallback (roughly central China / 35°N)
double g_longitude = 0.0;
bool g_geoAvailable = false; // True when location is successfully obtained

// ========== Get location using Windows.Devices.Geolocation (asynchronous) ==========
void RequestGeolocation(HWND hWnd) {
    try {
        Geolocator^ geolocator = ref new Geolocator();
        geolocator->DesiredAccuracyInMeters = 1000;  // 1 km accuracy, saves power

        IAsyncOperation<Geoposition^>^ operation = geolocator->GetGeopositionAsync();

        auto task = concurrency::create_task(operation);
        task.then([hWnd](Geoposition^ position) {
            if (position != nullptr) {
                double lat = position->Coordinate->Point->Position.Latitude;
                double lon = position->Coordinate->Point->Position.Longitude;
                g_latitude = lat;
                g_longitude = lon;
                g_geoAvailable = true;
                PostMessage(hWnd, WM_GEO_UPDATE, 0, 0); // success
            }
            else {
                g_geoAvailable = false;
                PostMessage(hWnd, WM_GEO_UPDATE, 0, 1); // failure
            }
            });
    }
    catch (Platform::Exception^ ex) {
        g_geoAvailable = false;
        PostMessage(hWnd, WM_GEO_UPDATE, 0, 1);
        // Optionally log ex->HResult for debugging
    }
}

// ========== Core theme switching ==========
bool SetSystemTheme(bool isDark) {
    HKEY hKey;
    auto result = RegOpenKeyExW(HKEY_CURRENT_USER,
        L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
        0, KEY_SET_VALUE, &hKey);
    if (result != ERROR_SUCCESS) {
        MessageBoxW(NULL, L"Cannot open registry key. Please check permissions.", L"Error", MB_OK | MB_ICONERROR);
        return false;
    }

    DWORD theme_val = isDark ? 0 : 1;

    result = RegSetValueExW(hKey, L"AppsUseLightTheme", 0, REG_DWORD, (const BYTE*)&theme_val, sizeof(DWORD));
    if (result != ERROR_SUCCESS) {
        RegCloseKey(hKey);
        MessageBoxW(NULL, L"Failed to write AppsUseLightTheme.", L"Error", MB_OK | MB_ICONERROR);
        return false;
    }

    result = RegSetValueExW(hKey, L"SystemUsesLightTheme", 0, REG_DWORD, (const BYTE*)&theme_val, sizeof(DWORD));
    if (result != ERROR_SUCCESS) {
        RegCloseKey(hKey);
        MessageBoxW(NULL, L"Failed to write SystemUsesLightTheme.", L"Error", MB_OK | MB_ICONERROR);
        return false;
    }

    RegCloseKey(hKey);

    SendMessageTimeoutW(HWND_BROADCAST, WM_SETTINGCHANGE, 0,
        (LPARAM)L"ImmersiveColorSet", SMTO_ABORTIFHUNG, 5000, NULL);
    return true;
}

// ========== Smart day/night detection based on latitude and day of year ==========
int GetDayOfYear(const SYSTEMTIME& st) {
    static int daysBeforeMonth[] = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };
    return daysBeforeMonth[st.wMonth - 1] + st.wDay;
}

void GetSunriseSunset(double latitude, double& sunrise, double& sunset) {
    SYSTEMTIME st;
    GetLocalTime(&st);
    int dayOfYear = GetDayOfYear(st);

    // Solar declination (approximate)
    double declination = 23.44 * sin((dayOfYear - 80) * (360.0 / 365.0) * 3.1415926 / 180.0);

    double latRad = latitude * 3.1415926 / 180.0;
    double decRad = declination * 3.1415926 / 180.0;

    double cosH = -tan(latRad) * tan(decRad);
    if (cosH > 1.0) cosH = 1.0;
    if (cosH < -1.0) cosH = -1.0;

    double hourAngle = acos(cosH) * 180.0 / 3.1415926;
    sunrise = 12.0 - hourAngle / 15.0;
    sunset = 12.0 + hourAngle / 15.0;
}

bool IsDaytime() {
    SYSTEMTIME st;
    GetLocalTime(&st);

    double sunrise, sunset;
    GetSunriseSunset(g_latitude, sunrise, sunset);

    double currentHour = st.wHour + st.wMinute / 60.0 + st.wSecond / 3600.0;
    return (currentHour >= sunrise && currentHour <= sunset);
}

// ========== Calculate milliseconds until the next sunrise/sunset ==========
DWORD GetMillisecondsUntilNextSwitch() {
    SYSTEMTIME st;
    GetLocalTime(&st);

    double sunrise, sunset;
    GetSunriseSunset(g_latitude, sunrise, sunset);

    double currentHour = st.wHour + st.wMinute / 60.0 + st.wSecond / 3600.0;
    double nextEventHour;

    if (currentHour < sunrise) {
        nextEventHour = sunrise;
    }
    else if (currentHour < sunset) {
        nextEventHour = sunset;
    }
    else {
        nextEventHour = sunrise + 24.0;
    }

    double diffHours = nextEventHour - currentHour;
    int diffMilliseconds = (int)(diffHours * 3600 * 1000);
    return (diffMilliseconds > 0) ? diffMilliseconds : 0;
}

void AutoUpdateTheme() {
    bool isDay = IsDaytime();
    SetSystemTheme(!isDay);
}

// ========== Auto-start management ==========
bool IsAutoStartEnabled() {
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_CURRENT_USER,
        L"Software\\Microsoft\\Windows\\CurrentVersion\\Run",
        0, KEY_READ, &hKey) == ERROR_SUCCESS) {
        WCHAR value[256];
        DWORD size = sizeof(value);
        DWORD type;
        LONG ret = RegQueryValueExW(hKey, L"AutoThemeSwitcher", NULL, &type, (BYTE*)value, &size);
        RegCloseKey(hKey);
        return (ret == ERROR_SUCCESS);
    }
    return false;
}

void SetAutoStart(bool enable) {
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_CURRENT_USER,
        L"Software\\Microsoft\\Windows\\CurrentVersion\\Run",
        0, KEY_SET_VALUE, &hKey) == ERROR_SUCCESS) {
        if (enable) {
            WCHAR path[MAX_PATH];
            GetModuleFileNameW(NULL, path, MAX_PATH);
            RegSetValueExW(hKey, L"AutoThemeSwitcher", 0, REG_SZ,
                (const BYTE*)path, (DWORD)(wcslen(path) * sizeof(WCHAR)));
            MessageBoxW(NULL, L"Auto-start has been enabled.", L"Info", MB_OK | MB_ICONINFORMATION);
        }
        else {
            RegDeleteValueW(hKey, L"AutoThemeSwitcher");
        }
        RegCloseKey(hKey);
    }
}

// ========== Window procedure ==========
LRESULT CALLBACK WndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_CREATE: {
        // Ask for auto-start if not yet set
        if (!IsAutoStartEnabled()) {
            int choice = MessageBoxW(NULL,
                L"Do you want to add this program to Windows startup?\n(You can change this later from the tray menu.)",
                L"Question", MB_YESNO | MB_ICONQUESTION);
            if (choice == IDYES) SetAutoStart(true);
        }

        // Request location asynchronously (will trigger system permission prompt)
        RequestGeolocation(hWnd);

        // Use default latitude until we get a real one
        DWORD delay = GetMillisecondsUntilNextSwitch();
        SetTimer(hWnd, TIMER_SWITCH, delay, NULL);
        AutoUpdateTheme();

        MessageBoxW(NULL, L"Acquiring your location for sunrise/sunset calculation...\n"
            L"Please allow the system location permission prompt.",
            L"Info", MB_OK | MB_ICONINFORMATION);
        break;
    }

    case WM_GEO_UPDATE: {
        if (lParam == 0) {
            // Success
            WCHAR msg[256];
            wsprintfW(msg, L"Location acquired: latitude %.2f°, longitude %.2f°", g_latitude, g_longitude);
            MessageBoxW(NULL, msg, L"Location Update", MB_OK | MB_ICONINFORMATION);

            // Recalculate timers and update theme
            KillTimer(hWnd, TIMER_SWITCH);
            DWORD delay = GetMillisecondsUntilNextSwitch();
            SetTimer(hWnd, TIMER_SWITCH, delay, NULL);
            AutoUpdateTheme();
        }
        else {
            // Failure (user denied or location unavailable)
            MessageBoxW(NULL, L"Could not obtain your location. Using default latitude (35°N).\n"
                L"You can enable location services in system settings and refresh from the tray menu.",
                L"Info", MB_OK | MB_ICONWARNING);
        }
        break;
    }

    case WM_TIMER:
        if (wParam == TIMER_SWITCH) {
            KillTimer(hWnd, TIMER_SWITCH);
            AutoUpdateTheme();
            DWORD delay = GetMillisecondsUntilNextSwitch();
            SetTimer(hWnd, TIMER_SWITCH, delay, NULL);
        }
        break;

    case WM_TRAYICON:
        if (lParam == WM_RBUTTONUP) {
            POINT pt;
            GetCursorPos(&pt);
            HMENU hMenu = CreatePopupMenu();
            AppendMenuW(hMenu, MF_STRING, ID_TRAY_TOGGLE, L"Toggle Theme Now");
            AppendMenuW(hMenu, MF_STRING, ID_TRAY_REFRESH, L"Refresh Location");
            AppendMenuW(hMenu, MF_STRING, ID_TRAY_EXIT, L"Exit");
            SetForegroundWindow(hWnd);
            TrackPopupMenu(hMenu, TPM_BOTTOMALIGN | TPM_LEFTALIGN, pt.x, pt.y, 0, hWnd, NULL);
            DestroyMenu(hMenu);
        }
        break;

    case WM_COMMAND:
        switch (LOWORD(wParam)) {
        case ID_TRAY_TOGGLE: {
            HKEY hKey;
            DWORD val;
            if (RegOpenKeyExW(HKEY_CURRENT_USER,
                L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                0, KEY_READ, &hKey) == ERROR_SUCCESS) {
                DWORD size = sizeof(DWORD);
                if (RegQueryValueExW(hKey, L"AppsUseLightTheme", NULL, NULL, (BYTE*)&val, &size) == ERROR_SUCCESS) {
                    RegCloseKey(hKey);
                    bool newDark = (val == 1);
                    if (SetSystemTheme(newDark)) {
                        MessageBoxW(NULL, L"Theme toggled successfully.", L"Info", MB_OK | MB_ICONINFORMATION);
                    }
                }
                else {
                    RegCloseKey(hKey);
                }
            }
            break;
        }
        case ID_TRAY_REFRESH: {
            RequestGeolocation(hWnd);
            MessageBoxW(NULL, L"Refreshing location...", L"Info", MB_OK | MB_ICONINFORMATION);
            break;
        }
        case ID_TRAY_EXIT:
            DestroyWindow(hWnd);
            break;
        }
        break;

    case WM_POWERBROADCAST:
        if (wParam == PBT_APMRESUMEAUTOMATIC) {
            KillTimer(hWnd, TIMER_SWITCH);
            DWORD delay = GetMillisecondsUntilNextSwitch();
            SetTimer(hWnd, TIMER_SWITCH, delay, NULL);
            AutoUpdateTheme();
        }
        break;

    case WM_TIMECHANGE:
        KillTimer(hWnd, TIMER_SWITCH);
        DWORD delay = GetMillisecondsUntilNextSwitch();
        SetTimer(hWnd, TIMER_SWITCH, delay, NULL);
        AutoUpdateTheme();
        break;

    case WM_DESTROY:
        KillTimer(hWnd, TIMER_SWITCH);
        Shell_NotifyIconW(NIM_DELETE, &nid);
        PostQuitMessage(0);
        break;

    default:
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }
    return 0;
}

// ========== Entry point ==========
int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow) {
    // Initialize COM (required for WinRT)
    HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
    if (FAILED(hr)) {
        MessageBoxW(NULL, L"COM initialization failed.", L"Error", MB_OK | MB_ICONERROR);
        return 1;
    }

    LPCWSTR className = L"AutoThemeSwitcherClass";
    WNDCLASSW wc = {};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = className;
    if (!RegisterClassW(&wc)) {
        MessageBoxW(NULL, L"Window class registration failed.", L"Error", MB_OK | MB_ICONERROR);
        CoUninitialize();
        return 1;
    }

    HWND hWnd = CreateWindowExW(0, className, L"Auto Theme Switcher", 0,
        0, 0, 0, 0, HWND_DESKTOP, NULL, hInstance, NULL);
    if (!hWnd) {
        MessageBoxW(NULL, L"Window creation failed.", L"Error", MB_OK | MB_ICONERROR);
        CoUninitialize();
        return 1;
    }

    // Add system tray icon
    nid.cbSize = sizeof(NOTIFYICONDATAW);
    nid.hWnd = hWnd;
    nid.uID = 1;
    nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    nid.uCallbackMessage = WM_TRAYICON;
    nid.hIcon = LoadIcon(NULL, IDI_APPLICATION);
    wcscpy_s(nid.szTip, L"Auto Theme Switcher");
    Shell_NotifyIconW(NIM_ADD, &nid);

    MSG msg;
    while (GetMessageW(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    CoUninitialize();
    return (int)msg.wParam;
}
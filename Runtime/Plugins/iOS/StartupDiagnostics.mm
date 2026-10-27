// Native side of LaunchScreen.cs on iOS.
#import "UnityAppController.h"

// Pixels of the Unity view per point of the screen, the unit in which the launch storyboard sizes the icon.
extern "C" float StartupDiagnostics_GetPixelsPerPoint()
{
    return (float)UnityGetGLView().contentScaleFactor;
}
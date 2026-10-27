package com.javienne.startupdiagnostics;

import android.app.Activity;
import android.app.Application;
import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.window.SplashScreenView;

/**
 * Removes the splash screen of Android 12 and later at once when the app draws its first frame, instead of the default
 * exit animation, which fades the icon out before fading the app in: the static splash view of Unity shows the same icon
 * at the same place, so that the launch screen goes on without a blink. The exit must be set while the activity is
 * created: this content provider, created with the app before any activity, registers for that. The launch screen of
 * Startup Diagnostics declares it in the manifest of the build only when it is on.
 */
public class SplashScreenExit extends ContentProvider implements Application.ActivityLifecycleCallbacks {
    @Override
    public boolean onCreate() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            ((Application) getContext().getApplicationContext()).registerActivityLifecycleCallbacks(this);
        }

        return true;
    }

    @Override
    public void onActivityCreated(Activity activity, Bundle savedInstanceState) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            activity.getSplashScreen().setOnExitAnimationListener(SplashScreenView::remove);
        }
    }

    @Override
    public void onActivityStarted(Activity activity) {
    }

    @Override
    public void onActivityResumed(Activity activity) {
    }

    @Override
    public void onActivityPaused(Activity activity) {
    }

    @Override
    public void onActivityStopped(Activity activity) {
    }

    @Override
    public void onActivitySaveInstanceState(Activity activity, Bundle outState) {
    }

    @Override
    public void onActivityDestroyed(Activity activity) {
    }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder) {
        return null;
    }

    @Override
    public String getType(Uri uri) {
        return null;
    }

    @Override
    public Uri insert(Uri uri, ContentValues values) {
        return null;
    }

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        return 0;
    }

    @Override
    public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) {
        return 0;
    }
}
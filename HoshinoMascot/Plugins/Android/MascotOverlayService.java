package com.hoshino.mascot;

import android.app.Activity;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.res.Configuration;
import android.graphics.Insets;
import android.graphics.PixelFormat;
import android.graphics.Rect;
import android.hardware.display.DisplayManager;
import android.net.Uri;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.provider.Settings;
import android.util.DisplayMetrics;
import android.util.Log;
import android.view.Display;
import android.view.Gravity;
import android.view.InputEvent;
import android.view.MotionEvent;
import android.view.SurfaceView;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.view.WindowMetrics;
import android.widget.FrameLayout;

import java.lang.reflect.Proxy;

/**
 * 호시노를 Android 홈 화면(다른 앱 위)에 띄우는 포그라운드 서비스입니다.
 *
 * 앱 화면의 Unity와 따로 돌도록 별도 프로세스(":overlay")에서 Unity를 하나 더 띄우고,
 * 그 화면을 "다른 앱 위에 표시" 창에 넣습니다. Android 12부터는 다른 앱 위의 창이 터치를
 * 아래로 넘기지 못하므로, 창을 캐릭터 주변만큼만 작게 만들어 캐릭터를 따라 옮깁니다.
 * 창의 위치와 크기는 Unity 쪽 MascotOverlay가 정합니다.
 *
 * Unity 버전마다 플레이어 클래스가 달라서(2022: UnityPlayer, 2023 이상: UnityPlayerForActivityOrService)
 * Unity 클래스는 리플렉션으로 부릅니다.
 */
public class MascotOverlayService extends Service {
    static final String TAG = "HoshinoOverlay";
    static final String CHANNEL_ID = "hoshino_overlay";
    static final String ACTION_STOP = "com.hoshino.mascot.STOP_OVERLAY";
    static final int NOTIFICATION_ID = 1101;

    // 이 프로세스에서 Unity를 오버레이로 띄웠는지. 앱 화면 프로세스에서는 늘 false입니다
    static volatile boolean hosting;
    static volatile MascotOverlayService instance;

    // Unity가 요청한 창 위치와 크기 (화면 왼쪽 위 기준 픽셀)
    static final Object lock = new Object();
    static int requestX, requestY, requestWidth, requestHeight;
    static boolean requestPending;
    // 실제로 적용된 창 [x, y, 너비, 높이]
    static volatile int[] appliedRect = new int[4];

    Handler handler;
    WindowManager windowManager;
    WindowManager.LayoutParams params;
    Object unityPlayer;
    View playerView;
    FrameLayout container;
    boolean attached;
    BroadcastReceiver screenReceiver;

    // ───────── 앱 화면(메인 프로세스)의 Unity에서 부릅니다 ─────────

    public static boolean canDrawOverlays(Context context) {
        return Build.VERSION.SDK_INT < 23 || Settings.canDrawOverlays(context);
    }

    /** "다른 앱 위에 표시" 설정 화면을 엽니다. */
    public static void requestPermission(Activity activity) {
        Intent intent = new Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION, Uri.parse("package:" + activity.getPackageName()));
        activity.startActivity(intent);
    }

    public static void start(Context context) {
        Intent intent = new Intent(context, MascotOverlayService.class);
        if (Build.VERSION.SDK_INT >= 26) context.startForegroundService(intent);
        else context.startService(intent);
    }

    /** 홈 화면의 호시노를 내립니다. 떠 있지 않으면 아무 일도 하지 않습니다. */
    public static void stop(Context context) {
        context.stopService(new Intent(context, MascotOverlayService.class));
    }

    // ───────── 오버레이 프로세스의 Unity(MascotOverlay)에서 부릅니다 ─────────

    public static boolean isHosting() {
        return hosting;
    }

    /** [화면 너비, 높이, 왼쪽, 위, 오른쪽, 아래 시스템 표시줄 두께] 픽셀 */
    public static int[] getDisplayInfo() {
        MascotOverlayService service = instance;
        return service != null ? service.readDisplayInfo() : null;
    }

    public static int[] getWindowRect() {
        return appliedRect.clone();
    }

    /** 창을 옮기거나 크기를 바꿉니다 (화면 왼쪽 위 기준 픽셀). Unity 스레드에서 불러도 됩니다. */
    public static void setWindowRect(int x, int y, int width, int height) {
        MascotOverlayService service = instance;
        if (service == null) return;
        synchronized (lock) {
            requestX = x;
            requestY = y;
            requestWidth = width;
            requestHeight = height;
            // 아직 처리 안 된 요청이 있으면 그 요청이 최신 값을 씁니다
            if (requestPending) return;
            requestPending = true;
        }
        service.handler.post(service::applyRequestedRect);
    }

    public static void stopFromUnity() {
        MascotOverlayService service = instance;
        if (service != null) service.handler.post(service::stopSelf);
    }

    // ───────── 서비스 ─────────

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onCreate() {
        super.onCreate();
        instance = this;
        hosting = true;
        handler = new Handler(Looper.getMainLooper());
        windowManager = createWindowManager();
        try {
            startForegroundWithNotification();
            createPlayer();
        } catch (Exception e) {
            Log.e(TAG, "Unity를 홈 화면 창에 띄우지 못했습니다", e);
            stopSelf();
        }
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent != null && ACTION_STOP.equals(intent.getAction())) stopSelf();
        return START_NOT_STICKY;
    }

    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        invoke(unityPlayer, "configurationChanged", new Class<?>[]{Configuration.class}, newConfig);
    }

    @Override
    public void onDestroy() {
        instance = null;
        handler.removeCallbacksAndMessages(null);
        if (screenReceiver != null) {
            unregisterReceiver(screenReceiver);
            screenReceiver = null;
        }
        if (attached) {
            try {
                windowManager.removeView(container);
            } catch (RuntimeException e) {
                Log.w(TAG, "창을 닫지 못했습니다", e);
            }
            attached = false;
        }
        if (Build.VERSION.SDK_INT >= 24) stopForeground(STOP_FOREGROUND_REMOVE);
        else stopForeground(true);
        super.onDestroy();
        // 이 프로세스는 오버레이 전용이라 Unity와 함께 끝냅니다 (Unity는 다시 시작할 수 없습니다)
        invoke(unityPlayer, "destroy", new Class<?>[0]);
        android.os.Process.killProcess(android.os.Process.myPid());
    }

    // ───────── 준비 ─────────

    static int overlayType() {
        return Build.VERSION.SDK_INT >= 26
                ? WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY
                : WindowManager.LayoutParams.TYPE_PHONE;
    }

    WindowManager createWindowManager() {
        if (Build.VERSION.SDK_INT >= 30) {
            // Android 11부터는 창을 띄울 때 그 창 종류에 맞는 Context를 쓰는 것이 맞습니다
            Display display = getSystemService(DisplayManager.class).getDisplay(Display.DEFAULT_DISPLAY);
            Context windowContext = createDisplayContext(display).createWindowContext(overlayType(), null);
            return windowContext.getSystemService(WindowManager.class);
        }
        return (WindowManager) getSystemService(WINDOW_SERVICE);
    }

    void startForegroundWithNotification() {
        NotificationManager manager = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
        if (Build.VERSION.SDK_INT >= 26) {
            NotificationChannel channel = new NotificationChannel(CHANNEL_ID, "홈 화면의 호시노", NotificationManager.IMPORTANCE_LOW);
            channel.setShowBadge(false);
            manager.createNotificationChannel(channel);
        }

        int immutable = Build.VERSION.SDK_INT >= 23 ? PendingIntent.FLAG_IMMUTABLE : 0;
        Intent stopIntent = new Intent(this, MascotOverlayService.class).setAction(ACTION_STOP);
        PendingIntent stop = PendingIntent.getService(this, 1, stopIntent, immutable);
        // 알림을 누르면 앱이 열리고, 앱이 홈 화면의 호시노를 데려옵니다
        Intent launch = getPackageManager().getLaunchIntentForPackage(getPackageName());
        PendingIntent open = launch != null ? PendingIntent.getActivity(this, 2, launch, immutable) : null;

        Notification.Builder builder = Build.VERSION.SDK_INT >= 26
                ? new Notification.Builder(this, CHANNEL_ID)
                : new Notification.Builder(this);
        int icon = getApplicationInfo().icon;
        builder.setSmallIcon(icon != 0 ? icon : android.R.drawable.star_on)
                .setContentTitle("호시노가 홈 화면에 있어요")
                .setContentText("누르면 앱으로 돌아가요")
                .setOngoing(true)
                .setContentIntent(open)
                .addAction(0, "끄기", stop);

        // Android 14부터는 매니페스트에 적힌 포그라운드 서비스 종류를 같이 넘겨야 합니다 (빌드 스크립트가 적습니다)
        int type = 0;
        if (Build.VERSION.SDK_INT >= 29) {
            try {
                type = getPackageManager().getServiceInfo(new ComponentName(this, MascotOverlayService.class), 0).getForegroundServiceType();
            } catch (Exception e) {
                Log.w(TAG, "포그라운드 서비스 종류를 읽지 못했습니다", e);
            }
        }
        if (Build.VERSION.SDK_INT >= 29 && type != 0) startForeground(NOTIFICATION_ID, builder.build(), type);
        else startForeground(NOTIFICATION_ID, builder.build());
    }

    void createPlayer() throws Exception {
        ClassLoader loader = getClassLoader();
        Class<?> listenerType = Class.forName("com.unity3d.player.IUnityPlayerLifecycleEvents", true, loader);
        Object listener = Proxy.newProxyInstance(loader, new Class<?>[]{listenerType}, (proxy, method, args) -> {
            switch (method.getName()) {
                case "onUnityPlayerUnloaded":
                case "onUnityPlayerQuitted":
                    handler.post(this::stopSelf);
                    return null;
                case "hashCode":
                    return System.identityHashCode(proxy);
                case "equals":
                    return proxy == args[0];
                case "toString":
                    return "HoshinoOverlayLifecycle";
                default:
                    return null;
            }
        });

        Class<?> playerType;
        try {
            playerType = Class.forName("com.unity3d.player.UnityPlayerForActivityOrService", true, loader);
        } catch (ClassNotFoundException e) {
            playerType = Class.forName("com.unity3d.player.UnityPlayer", true, loader);
        }
        unityPlayer = playerType.getConstructor(Context.class, listenerType).newInstance(this, listener);
        playerView = unityPlayer instanceof View ? (View) unityPlayer : findPlayerView(unityPlayer);
        makeTranslucent(playerView);

        // Unity 화면이 직접 처리하지 않은 터치는 Unity에 넘깁니다 (Unity의 Activity가 하는 것과 같습니다)
        container = new FrameLayout(this) {
            @Override
            public boolean onTouchEvent(MotionEvent event) {
                return forwardToPlayer(event);
            }
        };
        container.addView(playerView, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));

        params = new WindowManager.LayoutParams(
                WindowManager.LayoutParams.WRAP_CONTENT,
                WindowManager.LayoutParams.WRAP_CONTENT,
                overlayType(),
                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                        | WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL
                        | WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN
                        | WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS
                        | WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED,
                PixelFormat.TRANSLUCENT);
        params.gravity = Gravity.TOP | Gravity.LEFT;
        if (Build.VERSION.SDK_INT >= 28) {
            params.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        }
        // 처음에는 화면 오른쪽 아래에 띄우고, 곧 Unity가 캐릭터에 맞춰 옮깁니다
        int[] display = readDisplayInfo();
        int size = Math.min(display[0], display[1]) / 2;
        params.width = size;
        params.height = size;
        params.x = display[0] - display[4] - size;
        params.y = display[1] - display[5] - size;
        windowManager.addView(container, params);
        attached = true;
        appliedRect = new int[]{params.x, params.y, params.width, params.height};
        // Unity가 그리는 면이 창에 붙은 뒤에 생기는 경우를 위해 한 번 더 맞춥니다
        handler.post(() -> makeTranslucent(playerView));

        invoke(unityPlayer, "onStart", new Class<?>[0]);
        resumePlayer();
        invoke(unityPlayer, "windowFocusChanged", new Class<?>[]{boolean.class}, true);
        registerScreenReceiver();
    }

    // Unity 2023 이상의 플레이어는 View가 아니고 화면을 따로 줍니다
    static View findPlayerView(Object player) throws Exception {
        for (String name : new String[]{"getFrameLayout", "getView"}) {
            try {
                return (View) player.getClass().getMethod(name).invoke(player);
            } catch (NoSuchMethodException e) {
                // 다음 이름을 봅니다
            }
        }
        throw new NoSuchMethodException("Unity 플레이어의 화면을 찾지 못했습니다: " + player.getClass().getName());
    }

    boolean forwardToPlayer(MotionEvent event) {
        try {
            Object handled = unityPlayer.getClass().getMethod("onTouchEvent", MotionEvent.class).invoke(unityPlayer, event);
            return handled instanceof Boolean && (Boolean) handled;
        } catch (NoSuchMethodException e) {
            return invoke(unityPlayer, "injectEvent", new Class<?>[]{InputEvent.class}, event);
        } catch (Exception e) {
            Log.w(TAG, "터치를 넘기지 못했습니다", e);
            return false;
        }
    }

    /** 캐릭터가 없는 곳은 뒤(홈 화면)가 보이도록 Unity가 그리는 면을 투명하게 합니다. */
    static void makeTranslucent(View view) {
        if (view instanceof SurfaceView) {
            SurfaceView surface = (SurfaceView) view;
            surface.getHolder().setFormat(PixelFormat.TRANSLUCENT);
            surface.setZOrderOnTop(true);
        }
        if (view instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) view;
            for (int i = 0; i < group.getChildCount(); i++) makeTranslucent(group.getChildAt(i));
        }
    }

    // 화면이 꺼져 있는 동안은 Unity를 멈춰서 배터리를 아낍니다
    void registerScreenReceiver() {
        screenReceiver = new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                if (Intent.ACTION_SCREEN_OFF.equals(intent.getAction())) pausePlayer();
                else resumePlayer();
            }
        };
        IntentFilter filter = new IntentFilter(Intent.ACTION_SCREEN_OFF);
        filter.addAction(Intent.ACTION_SCREEN_ON);
        // 4 = Context.RECEIVER_NOT_EXPORTED (Android 13에 생긴 값이라 숫자로 씁니다)
        if (Build.VERSION.SDK_INT >= 33) registerReceiver(screenReceiver, filter, 4);
        else registerReceiver(screenReceiver, filter);
    }

    // Unity 2023 이상은 onResume/onPause, 그 전은 resume/pause입니다
    void resumePlayer() {
        if (!invoke(unityPlayer, "onResume", new Class<?>[0])) invoke(unityPlayer, "resume", new Class<?>[0]);
    }

    void pausePlayer() {
        if (!invoke(unityPlayer, "onPause", new Class<?>[0])) invoke(unityPlayer, "pause", new Class<?>[0]);
    }

    // ───────── 창 ─────────

    void applyRequestedRect() {
        int x, y, width, height;
        synchronized (lock) {
            x = requestX;
            y = requestY;
            width = requestWidth;
            height = requestHeight;
            requestPending = false;
        }
        if (!attached || width <= 0 || height <= 0) return;
        if (params.x == x && params.y == y && params.width == width && params.height == height) return;
        params.x = x;
        params.y = y;
        params.width = width;
        params.height = height;
        try {
            windowManager.updateViewLayout(container, params);
        } catch (RuntimeException e) {
            Log.w(TAG, "창을 옮기지 못했습니다", e);
            return;
        }
        appliedRect = new int[]{x, y, width, height};
    }

    int[] readDisplayInfo() {
        if (Build.VERSION.SDK_INT >= 30) {
            WindowMetrics metrics = windowManager.getCurrentWindowMetrics();
            Rect bounds = metrics.getBounds();
            Insets insets = metrics.getWindowInsets().getInsetsIgnoringVisibility(
                    WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout());
            return new int[]{bounds.width(), bounds.height(), insets.left, insets.top, insets.right, insets.bottom};
        }
        DisplayMetrics metrics = new DisplayMetrics();
        windowManager.getDefaultDisplay().getRealMetrics(metrics);
        int width = metrics.widthPixels;
        int height = metrics.heightPixels;
        int statusBar = dimension("status_bar_height");
        // 옛 Android의 가로 화면에서는 내비게이션 바가 보통 오른쪽에 있습니다
        if (width > height) return new int[]{width, height, 0, statusBar, dimension("navigation_bar_width"), 0};
        return new int[]{width, height, 0, statusBar, 0, dimension("navigation_bar_height")};
    }

    int dimension(String name) {
        int id = getResources().getIdentifier(name, "dimen", "android");
        return id != 0 ? getResources().getDimensionPixelSize(id) : 0;
    }

    /** Unity 플레이어의 메서드를 이름으로 부릅니다. 이 Unity 버전에 그 메서드가 있으면 true. */
    static boolean invoke(Object target, String name, Class<?>[] types, Object... args) {
        if (target == null) return false;
        try {
            target.getClass().getMethod(name, types).invoke(target, args);
            return true;
        } catch (NoSuchMethodException e) {
            return false;
        } catch (Exception e) {
            Log.w(TAG, name + " 호출 실패", e);
            return true;
        }
    }
}

package com.tools.sleep_timer

import android.Manifest
import android.bluetooth.BluetoothAdapter
import android.bluetooth.BluetoothManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.media.AudioAttributes
import android.media.AudioFocusRequest
import android.media.AudioManager
import android.net.wifi.WifiManager
import android.os.Build
import android.provider.Settings
import androidx.annotation.NonNull
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {

    private val CHANNEL = "com.tools.sleep_timer/system_control"
    private val BLUETOOTH_PERMISSION_REQUEST_CODE = 1001

    private var pendingResult: MethodChannel.Result? = null
    private var pendingAction: String? = null
    private var audioFocusRequest: AudioFocusRequest? = null

    override fun configureFlutterEngine(@NonNull flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL).setMethodCallHandler { call, result ->
            when (call.method) {
                "pauseMusic" -> handlePauseMusic(result)
                "disableBluetooth" -> handleDisableBluetooth(result)
                "disableWifi" -> handleDisableWifi(result)
                "getBluetoothState" -> handleGetBluetoothState(result)
                "getWifiState" -> handleGetWifiState(result)
                "isMusicPlaying" -> handleIsMusicPlaying(result)
                else -> result.notImplemented()
            }
        }
    }

    private fun handlePauseMusic(result: MethodChannel.Result) {
        try {
            val audioManager = getSystemService(Context.AUDIO_SERVICE) as AudioManager
            val granted: Int = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                val attributes = AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_MEDIA)
                    .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                    .build()
                val focusRequest = AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN_TRANSIENT)
                    .setAudioAttributes(attributes)
                    .setOnAudioFocusChangeListener { }
                    .build()
                audioFocusRequest = focusRequest
                audioManager.requestAudioFocus(focusRequest)
            } else {
                @Suppress("DEPRECATION")
                audioManager.requestAudioFocus(
                    null,
                    AudioManager.STREAM_MUSIC,
                    AudioManager.AUDIOFOCUS_GAIN_TRANSIENT
                )
            }
            result.success(granted == AudioManager.AUDIOFOCUS_REQUEST_GRANTED)
        } catch (e: SecurityException) {
            result.error("SECURITY_EXCEPTION", e.message, null)
        } catch (e: Exception) {
            result.error("PAUSE_MUSIC_ERROR", e.message, null)
        }
    }

    private fun getBluetoothAdapter(): BluetoothAdapter? {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            val bm = getSystemService(Context.BLUETOOTH_SERVICE) as? BluetoothManager
            bm?.adapter
        } else {
            @Suppress("DEPRECATION")
            BluetoothAdapter.getDefaultAdapter()
        }
    }

    private fun handleDisableBluetooth(result: MethodChannel.Result) {
        try {
            val adapter = getBluetoothAdapter()
            if (adapter == null) {
                result.success(true)
                return
            }
            if (!adapter.isEnabled) {
                result.success(true)
                return
            }

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                if (ContextCompat.checkSelfPermission(
                        this,
                        Manifest.permission.BLUETOOTH_CONNECT
                    ) != PackageManager.PERMISSION_GRANTED
                ) {
                    pendingResult = result
                    pendingAction = "disableBluetooth"
                    ActivityCompat.requestPermissions(
                        this,
                        arrayOf(Manifest.permission.BLUETOOTH_CONNECT),
                        BLUETOOTH_PERMISSION_REQUEST_CODE
                    )
                    return
                }
            }

            @Suppress("DEPRECATION")
            val disabled = adapter.disable()
            result.success(disabled || !adapter.isEnabled)
        } catch (e: SecurityException) {
            result.error("SECURITY_EXCEPTION", e.message, null)
        } catch (e: Exception) {
            result.error("DISABLE_BLUETOOTH_ERROR", e.message, null)
        }
    }

    private fun handleDisableWifi(result: MethodChannel.Result) {
        try {
            val wifiManager = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
            if (Build.VERSION.SDK_INT <= Build.VERSION_CODES.P) {
                @Suppress("DEPRECATION")
                val ok = wifiManager.setWifiEnabled(false)
                result.success(ok)
            } else {
                var ok = false
                try {
                    @Suppress("DEPRECATION")
                    ok = wifiManager.setWifiEnabled(false)
                } catch (_: Exception) {
                    ok = false
                }
                if (ok) {
                    result.success(true)
                } else {
                    val intent = Intent(Settings.Panel.ACTION_WIFI)
                    intent.flags = Intent.FLAG_ACTIVITY_NEW_TASK
                    startActivity(intent)
                    result.success("settings")
                }
            }
        } catch (e: SecurityException) {
            result.error("SECURITY_EXCEPTION", e.message, null)
        } catch (e: Exception) {
            result.error("DISABLE_WIFI_ERROR", e.message, null)
        }
    }

    private fun handleGetBluetoothState(result: MethodChannel.Result) {
        try {
            val adapter = getBluetoothAdapter()
            result.success(adapter?.isEnabled == true)
        } catch (e: SecurityException) {
            result.error("SECURITY_EXCEPTION", e.message, null)
        } catch (e: Exception) {
            result.error("GET_BLUETOOTH_STATE_ERROR", e.message, null)
        }
    }

    private fun handleGetWifiState(result: MethodChannel.Result) {
        try {
            val wifiManager = applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
            result.success(wifiManager.isWifiEnabled)
        } catch (e: SecurityException) {
            result.error("SECURITY_EXCEPTION", e.message, null)
        } catch (e: Exception) {
            result.error("GET_WIFI_STATE_ERROR", e.message, null)
        }
    }

    private fun handleIsMusicPlaying(result: MethodChannel.Result) {
        try {
            val audioManager = getSystemService(Context.AUDIO_SERVICE) as AudioManager
            result.success(audioManager.isMusicActive)
        } catch (e: Exception) {
            result.error("IS_MUSIC_PLAYING_ERROR", e.message, null)
        }
    }

    override fun onRequestPermissionsResult(
        requestCode: Int,
        permissions: Array<out String>,
        grantResults: IntArray
    ) {
        if (requestCode == BLUETOOTH_PERMISSION_REQUEST_CODE) {
            val result = pendingResult
            val action = pendingAction
            pendingResult = null
            pendingAction = null

            val granted = grantResults.isNotEmpty() &&
                grantResults[0] == PackageManager.PERMISSION_GRANTED

            if (result != null && action == "disableBluetooth") {
                if (!granted) {
                    result.error(
                        "PERMISSION_DENIED",
                        "BLUETOOTH_CONNECT permission denied",
                        null
                    )
                    super.onRequestPermissionsResult(requestCode, permissions, grantResults)
                    return
                }
                try {
                    val adapter = getBluetoothAdapter()
                    if (adapter == null || !adapter.isEnabled) {
                        result.success(true)
                    } else {
                        @Suppress("DEPRECATION")
                        val disabled = adapter.disable()
                        result.success(disabled || !adapter.isEnabled)
                    }
                } catch (e: SecurityException) {
                    result.error("SECURITY_EXCEPTION", e.message, null)
                } catch (e: Exception) {
                    result.error("DISABLE_BLUETOOTH_ERROR", e.message, null)
                }
            }
        }
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
    }
}

package com.unity3d.player;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.util.Log;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import com.unity3d.player.UnityPlayerGameActivity;

public class PrivacyActivity extends Activity implements DialogInterface.OnClickListener {
    private static final String TAG = "PrivacyActivity";
    private static final String PREFS_NAME = "PlayerPrefs";
    private static final String ACCEPTED_KEY = "PrivacyAcceptedKey";
    private static final String PRIVACY_POLICY_ASSET_URL = "file:///android_asset/bin/Data/StreamingAssets/privacy-policy-template.htm";
                
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Log.d(TAG, "onCreate: PrivacyActivity started");

        // 如果已经同意过隐私协议，则直接进入Unity Activity
        if (getPrivacyAccepted()) {
            Log.d(TAG, "onCreate: User already accepted privacy policy");
            enterUnityActivity();
            return;
        }

        // 弹出隐私协议对话框
        showPrivacyDialog();
    }

    // 显示隐私协议对话框
    private void showPrivacyDialog() {
        Log.d(TAG, "showPrivacyDialog: Displaying privacy dialog");
        
        WebView webView = new WebView(this);
        webView.getSettings().setJavaScriptEnabled(true); // 启用JavaScript以支持链接跳转
        webView.getSettings().setAllowFileAccess(true);
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url) {
                view.loadUrl(url); // 在WebView内打开链接
                return true;
            }
        });
        webView.loadUrl(PRIVACY_POLICY_ASSET_URL);
        
        AlertDialog.Builder privacyDialog = new AlertDialog.Builder(this);
        privacyDialog.setCancelable(false); // 禁止通过返回键取消
        privacyDialog.setView(webView);
        privacyDialog.setTitle("提示");
        privacyDialog.setNegativeButton("拒绝", this);
        privacyDialog.setPositiveButton("同意", this);
        privacyDialog.create().show();
    }

    @Override
    public void onClick(DialogInterface dialogInterface, int which) 
    {
        switch (which) {
            case AlertDialog.BUTTON_POSITIVE: // 点击同意按钮
                Log.d(TAG, "onClick: User accepted privacy policy");
                setPrivacyAccepted(true);
                enterUnityActivity();
                finish(); // 关闭当前Activity，防止返回
                break;
            case AlertDialog.BUTTON_NEGATIVE: // 点击拒绝按钮
                Log.d(TAG, "onClick: User declined privacy policy");
                finish(); // 退出应用
                break;
        }
    }

    // 启动Unity Activity
    private void enterUnityActivity() {
        Log.d(TAG, "enterUnityActivity: Starting UnityPlayerGameActivity");
        Intent unityIntent = new Intent(this, UnityPlayerGameActivity.class);
        startActivity(unityIntent);
    }

    // 本地存储保存同意隐私协议状态
    private void setPrivacyAccepted(boolean accepted) {
        SharedPreferences.Editor editor = getSharedPreferences(PREFS_NAME, MODE_PRIVATE).edit();
        editor.putBoolean(ACCEPTED_KEY, accepted);
        editor.apply();
        Log.d(TAG, "setPrivacyAccepted: Saved preference: " + accepted);
    }

    // 获取是否已经同意过
    private boolean getPrivacyAccepted() {
        SharedPreferences prefs = getSharedPreferences(PREFS_NAME, MODE_PRIVATE);
        boolean accepted = prefs.getBoolean(ACCEPTED_KEY, false);
        Log.d(TAG, "getPrivacyAccepted: " + accepted);
        return accepted;
    }

    // 禁止通过返回键绕过隐私协议
    @Override
    public void onBackPressed() {
        // 直接退出应用，不允许返回
        finish();
    }
}    

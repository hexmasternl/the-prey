import java.util.Properties

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
    alias(libs.plugins.kotlin.serialization)
}

// CI passes these as Gradle properties (ORG_GRADLE_PROJECT_versionCode / _versionName).
// Play rejects an upload whose versionCode is not higher than the last published one.
val appVersionCode = (findProperty("versionCode") ?: "1").toString().toInt()
val appVersionName = (findProperty("versionName") ?: "0.1.0").toString()

// Auth0 and backend settings. Override per build with -P<name>=<value> or ORG_GRADLE_PROJECT_<name>.
// The client id of a public (native) Auth0 application is not a secret.
val auth0Domain = (findProperty("auth0Domain") ?: "theprey.eu.auth0.com").toString()
val auth0ClientId = (findProperty("auth0ClientId") ?: "EnbXbZKCTImrr5dALFT0VLD10O0X3w5Q").toString()
val auth0Audience = (findProperty("auth0Audience") ?: "https://api.theprey.nl").toString()
// "https" uses Android App Links (needs the signing fingerprint registered in Auth0);
// a custom scheme such as "theprey" works without it, for local debug builds only.
val auth0Scheme = (findProperty("auth0Scheme") ?: "https").toString()
val apiBaseUrl = (findProperty("apiBaseUrl") ?: "https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io/").toString()

// Release signing is optional locally: only configured when key.properties exists.
val keystorePropertiesFile = rootProject.file("key.properties")
val keystoreProperties = Properties().apply {
    if (keystorePropertiesFile.exists()) keystorePropertiesFile.inputStream().use(::load)
}

android {
    namespace = "nl.hexmaster.theprey"
    compileSdk = 37

    defaultConfig {
        // Same id as the previously published app, so releases update the existing Play listing.
        applicationId = "nl.hexmaster.theprey"
        minSdk = 26
        targetSdk = 37
        versionCode = appVersionCode
        versionName = appVersionName

        buildConfigField("String", "AUTH0_DOMAIN", "\"$auth0Domain\"")
        buildConfigField("String", "AUTH0_CLIENT_ID", "\"$auth0ClientId\"")
        buildConfigField("String", "AUTH0_AUDIENCE", "\"$auth0Audience\"")
        buildConfigField("String", "AUTH0_SCHEME", "\"$auth0Scheme\"")
        buildConfigField("String", "API_BASE_URL", "\"$apiBaseUrl\"")
        buildConfigField("String", "PLAY_STORE_URL", "\"https://play.google.com/store/apps/details?id=$applicationId\"")
        manifestPlaceholders["auth0Domain"] = auth0Domain
        manifestPlaceholders["auth0Scheme"] = auth0Scheme
    }

    signingConfigs {
        if (keystorePropertiesFile.exists()) {
            create("release") {
                storeFile = rootProject.file(keystoreProperties.getProperty("storeFile"))
                storePassword = keystoreProperties.getProperty("storePassword")
                keyAlias = keystoreProperties.getProperty("keyAlias")
                keyPassword = keystoreProperties.getProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            signingConfig = signingConfigs.findByName("release")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    androidResources {
        // Locale config for the per-app language (Android 13+ system settings), from res/values* + resources.properties.
        generateLocaleConfig = true
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }

    testOptions {
        // Lets JVM tests construct Android framework types (e.g. a Context stand-in) without Robolectric.
        unitTests.isReturnDefaultValues = true
    }
}

dependencies {
    implementation(libs.androidx.core.ktx)
    implementation(libs.androidx.core.splashscreen)
    implementation(libs.androidx.appcompat)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.lifecycle.runtime.ktx)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.navigation.compose)
    implementation(libs.androidx.datastore.preferences)
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.graphics)
    implementation(libs.androidx.compose.ui.tooling.preview)
    implementation(libs.androidx.compose.material3)
    implementation(libs.auth0)
    implementation(libs.okhttp)
    implementation(libs.retrofit)
    implementation(libs.retrofit.kotlinx.serialization)
    implementation(libs.kotlinx.serialization.json)
    implementation(libs.play.services.location)
    implementation(libs.maplibre.android.sdk)
    debugImplementation(libs.androidx.compose.ui.tooling)

    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
}

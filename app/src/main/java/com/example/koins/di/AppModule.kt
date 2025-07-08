package com.example.koins.di

import com.example.koins.data.remote.service.BookShopService
import com.example.koins.data.remote.service.CoinPaprikaService
import com.example.koins.data.repo.CoinRepoImpl
import com.example.koins.domain.repo.CoinRepo
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import okhttp3.CertificatePinner
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object AppModule {

    @Provides
    @Singleton
    fun getCoinPaprikaService() : CoinPaprikaService{
        return Retrofit.Builder()
            .baseUrl("https://api.coinpaprika.com/")
            .addConverterFactory(GsonConverterFactory.create())
            .build()
            .create(CoinPaprikaService::class.java)
    }

    @Provides
    @Singleton
    fun getBookShopService() : BookShopService{
        val hostname = "192.168.1.8"
        val publicKey = "sha256/iRbXQIm6KB8qGPLZtAEfOuAqwbgV50U38dnkxlq+kLY="
        val certificatePin = CertificatePinner.Builder()
            .add(hostname, publicKey)
            .build()

        val loggingInterceptor = HttpLoggingInterceptor().apply {
            level = HttpLoggingInterceptor.Level.BODY
        }

        val client = OkHttpClient.Builder()
            .certificatePinner(certificatePin)
            .hostnameVerifier { hoastname, session ->
                hostname == "$hoastname"
            }
            .addInterceptor(loggingInterceptor)
            .build()

        return Retrofit.Builder()
            .baseUrl("https://$hostname:8080/")
            .client(client)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
            .create(BookShopService::class.java)
    }

    @Provides
    @Singleton
    fun getCoinRepo(coinPaprikaService: CoinPaprikaService) : CoinRepo{
        return CoinRepoImpl(coinPaprikaService)
    }
}
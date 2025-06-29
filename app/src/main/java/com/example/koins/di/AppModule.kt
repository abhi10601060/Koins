package com.example.koins.di

import com.example.koins.data.remote.service.CoinPaprikaService
import com.example.koins.data.repo.CoinRepoImpl
import com.example.koins.domain.repo.CoinRepo
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
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
    fun getCoinRepo(coinPaprikaService: CoinPaprikaService) : CoinRepo{
        return CoinRepoImpl(coinPaprikaService)
    }
}
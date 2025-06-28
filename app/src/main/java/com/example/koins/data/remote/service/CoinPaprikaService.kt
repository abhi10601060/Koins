package com.example.koins.data.remote.service

import com.example.koins.data.remote.dto.CoinDetailsDto
import com.example.koins.data.remote.dto.CoinDto
import retrofit2.Response
import retrofit2.http.GET
import retrofit2.http.Path

interface CoinPaprikaService {

    @GET("v1/coins")
    suspend fun getCoins() : Response<List<CoinDto>>

    @GET("v1/coins/{coinId}")
    suspend fun getCoin(@Path("coinId") coinId : String) : Response<CoinDetailsDto>
}
package com.example.koins.domain.repo

import com.example.koins.data.remote.dto.CoinDetailsDto
import com.example.koins.data.remote.dto.CoinDto
import retrofit2.Response

interface CoinRepo {

    suspend fun getCoins() : Response<List<CoinDto>>

    suspend fun getCoinById(coinId : String) : Response<CoinDetailsDto>

}
package com.example.koins.data.repo

import com.example.koins.data.remote.dto.CoinDetailsDto
import com.example.koins.data.remote.dto.CoinDto
import com.example.koins.data.remote.service.CoinPaprikaService
import com.example.koins.domain.model.Coin
import com.example.koins.domain.model.CoinDetails
import com.example.koins.domain.repo.CoinRepo
import retrofit2.Response
import javax.inject.Inject

class CoinRepoImpl @Inject constructor(
    private val coinPaprikaService: CoinPaprikaService
) : CoinRepo {
    override suspend fun getCoins(): Response<List<CoinDto>> {
        return coinPaprikaService.getCoins()
    }

    override suspend fun getCoinById(coinId: String): Response<CoinDetailsDto> {
        return coinPaprikaService.getCoin(coinId)
    }

}
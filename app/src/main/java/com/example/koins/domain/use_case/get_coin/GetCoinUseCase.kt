package com.example.koins.domain.use_case.get_coin

import coil3.network.HttpException
import com.example.koins.common.Resource
import com.example.koins.common.handleResource
import com.example.koins.data.remote.dto.CoinDetailsDto
import com.example.koins.data.remote.dto.toCoinDetails
import com.example.koins.domain.model.CoinDetails
import com.example.koins.domain.repo.CoinRepo
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import okio.IOException
import javax.inject.Inject

class GetCoinUseCase @Inject constructor(
    private val coinRepo: CoinRepo
) {
    operator fun invoke(coinId : String) : Flow<Resource<CoinDetails>> = flow {
        try {
            emit(Resource.Loading())
            val res = handleResource(coinRepo.getCoinById(coinId), "get Coin details For $coinId")
            when(res){
                is Resource.Success -> {
                    emit(Resource.Success(res.data!!.toCoinDetails()))
                }
                else -> {
                    emit(Resource.Error(res.message ?: "Internal server error"))
                }
            }
        }
        catch (e : HttpException){
            emit(Resource.Error("Internal Server Error..."))
        }
        catch (e : IOException){
            emit(Resource.Error("Couldn't reach server, Please check internet connection..."))
        }
        catch (e : Exception){
            emit(Resource.Error("Something went wrong..."))
        }
    }
}
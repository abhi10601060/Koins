package com.example.koins.domain.use_case.get_coins

import com.example.koins.common.Resource
import com.example.koins.common.handleResource
import com.example.koins.data.remote.dto.toCoin
import com.example.koins.domain.model.Coin
import com.example.koins.domain.repo.CoinRepo
import jakarta.inject.Inject
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import okio.IOException
import retrofit2.HttpException

class GetCoinsUseCase @Inject constructor(
    private val coinRepo: CoinRepo
) {
     operator fun invoke() : Flow<Resource<List<Coin>>> = flow {
        try {
            emit(Resource.Loading())
            val res = handleResource(coinRepo.getCoins(), "Get coin list")
            when(res){
                is Resource.Success -> {
                    emit(Resource.Success(res.data!!.map { it.toCoin() }))
                }
                else -> {
                    emit(Resource.Error(res.message ?: "Internal server error"))
                }
            }
        }
        catch (httpException : HttpException){
            emit(Resource.Error("Internal Server Error..."))
        }
        catch (ioException : IOException){
            emit(Resource.Error("Couldn't reach server, Please check internet connection..."))
        }
        catch (e : Exception){
            emit(Resource.Error("Something went wrong..."))
        }
    }
}
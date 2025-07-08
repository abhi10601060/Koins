package com.example.koins.domain.use_case.get_books

import com.example.koins.common.Resource
import com.example.koins.common.handleResource
import com.example.koins.data.remote.dto.BooksDto
import com.example.koins.data.remote.dto.toBooks
import com.example.koins.data.remote.dto.toCoinDetails
import com.example.koins.data.repo.BookShopRepoImpl
import com.example.koins.domain.model.Books
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import okio.IOException
import retrofit2.HttpException
import javax.inject.Inject

class GetBooksUseCase @Inject constructor(
    private val repository: BookShopRepoImpl
) {

    operator fun invoke() : Flow<Resource<Books>> = flow{
        try {
            emit(Resource.Loading())
            val res = handleResource(repository.getBooks(), "get books")
            when(res){
                is Resource.Success -> {
                    emit(Resource.Success(data =  res.data!!.toBooks()))
                }
                else -> {
                    emit(Resource.Error(res.message ?: "Internal server error"))
                }
            }
        }
        catch (ioE : IOException){
            emit(Resource.Error("Check your internet connection"))
        }
        catch (httpE : HttpException){
            emit(Resource.Error("unable to connect to server"))
        }
        catch (e : Exception){
            emit(Resource.Error("Unknown error occurred"))
        }
    }
}
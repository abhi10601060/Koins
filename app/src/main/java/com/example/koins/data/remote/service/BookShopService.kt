package com.example.koins.data.remote.service

import com.example.koins.data.remote.dto.BooksDto
import retrofit2.Response
import retrofit2.http.GET

interface BookShopService {

    @GET("/")
    suspend fun getBooks() : Response<BooksDto>
}
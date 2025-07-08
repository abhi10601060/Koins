package com.example.koins.domain.repo

import com.example.koins.data.remote.dto.BooksDto
import retrofit2.Response

interface BooksShopRepo {
    suspend fun getBooks() : Response<BooksDto>
}
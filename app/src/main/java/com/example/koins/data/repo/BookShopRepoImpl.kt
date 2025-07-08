package com.example.koins.data.repo

import com.example.koins.data.remote.dto.BooksDto
import com.example.koins.data.remote.service.BookShopService
import com.example.koins.domain.repo.BooksShopRepo
import retrofit2.Response
import javax.inject.Inject

class BookShopRepoImpl @Inject constructor(
    private val bookshopService: BookShopService
) : BooksShopRepo{

     override suspend fun getBooks() : Response<BooksDto> {
        return bookshopService.getBooks()
    }
}
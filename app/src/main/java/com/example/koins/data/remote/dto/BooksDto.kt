package com.example.koins.data.remote.dto

import com.example.koins.domain.model.Book
import com.example.koins.domain.model.Books
import com.google.gson.annotations.SerializedName

class BooksDto {
    @SerializedName("books")
    val books : List<BookDto>? = null
}

fun BooksDto.toBooks() = Books(books?.map { it.toBook() })

data class BookDto(
    @SerializedName("id")
    val id: Int = 0,
    @SerializedName("name")
    val name: String? = null,
)

fun BookDto.toBook() = Book(id , name ?: "name not found")